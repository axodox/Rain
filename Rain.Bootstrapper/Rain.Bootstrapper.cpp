#include <SDKDDKVer.h>
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <metahost.h>
#include <Shlwapi.h>
#include <string>

#include <objbase.h>
#include <mscoree.h>

#import "C:\Windows\Microsoft.NET\Framework\v4.0.30319\mscorlib.tlb" raw_interfaces_only

#pragma comment(lib, "mscoree.lib")
#pragma comment(lib, "shlwapi")

using namespace std;
using namespace mscorlib;

BOOL isLoaded = false;

class Bootstrapper
{
public:
  Bootstrapper(LPCTSTR clientPath);
  ~Bootstrapper();
private:
	BOOL _isEnabled;
  ICorRuntimeHost *_corRuntimeHost;
  BSTR _clientPath;
  BSTR _typeName;
	HANDLE _workerThread;
	static DWORD WINAPI LoadWorker(LPVOID parameter);
	VOID Load();	
} *bootstrapper;

Bootstrapper::Bootstrapper(LPCTSTR clientPath)
{
  ICLRMetaHost *metaHost = NULL;
  ICLRRuntimeInfo *runtimeInfo = NULL;
  ICLRRuntimeHost *clrRuntimeHost = NULL;

  CLRCreateInstance(CLSID_CLRMetaHost, IID_PPV_ARGS(&metaHost));
  metaHost->GetRuntime(L"v4.0.30319", IID_PPV_ARGS(&runtimeInfo));
  runtimeInfo->GetInterface(CLSID_CLRRuntimeHost, IID_PPV_ARGS(&clrRuntimeHost));
  clrRuntimeHost->Start();
  
  runtimeInfo->GetInterface(CLSID_CorRuntimeHost, IID_PPV_ARGS(&_corRuntimeHost));

  clrRuntimeHost->Release();
  runtimeInfo->Release();
  metaHost->Release();

  _clientPath = SysAllocString(clientPath);
  _typeName = SysAllocString(L"Rain.Client.Loader");

	_isEnabled = true;
	_workerThread = CreateThread(NULL, 0, &Bootstrapper::LoadWorker, this, 0, NULL);
}

DWORD WINAPI Bootstrapper::LoadWorker(LPVOID parameter)
{
	auto bootstrapper = (Bootstrapper*)parameter;
	while (bootstrapper->_isEnabled)
	{
		bootstrapper->Load();
		Sleep(1000);
	}
	return 0;
}

VOID Bootstrapper::Load()
{
  HDOMAINENUM domainEnum;
  _corRuntimeHost->EnumDomains(&domainEnum);
  
  IUnknown *lastDomainItem = NULL;
  while (true)
  {
    IUnknown *domainItem;
    _AppDomain *appDomain;

    _corRuntimeHost->NextDomain(domainEnum, &domainItem);

    if (lastDomainItem == domainItem)
    {
      break;
    }
    lastDomainItem = domainItem;

    domainItem->QueryInterface<_AppDomain>(&appDomain);

    _ObjectHandle *loader;
    appDomain->CreateInstanceFrom(_clientPath, _typeName, &loader);

    appDomain->Release();
    domainItem->Release();
  }
  _corRuntimeHost->CloseEnum(domainEnum);
}

Bootstrapper::~Bootstrapper()
{
	_isEnabled = false;
	WaitForSingleObject(_workerThread, INFINITE);

  _corRuntimeHost->Release();
  SysFreeString(_clientPath);
  SysFreeString(_typeName);
}

__declspec(dllexport) HRESULT LoadDotNetLibrary(LPCTSTR clientPath)
{
  if (isLoaded)
  {
    return 0;
  }
	isLoaded = true;

	bootstrapper = new Bootstrapper(clientPath);

  return 0;
}

BOOL APIENTRY DllMain(HMODULE hModule,
  DWORD  ul_reason_for_call,
  LPVOID lpReserved
  )
{
  switch (ul_reason_for_call)
  {
  
  case DLL_PROCESS_ATTACH:
		bootstrapper = nullptr;
    break;
  case DLL_PROCESS_DETACH:
		if (bootstrapper)
		{
			delete bootstrapper;
			bootstrapper = nullptr;
		}
    break;
  }
  return TRUE;
}
