#include <SDKDDKVer.h>

#include <stdio.h>
#include <stdlib.h>
#include <tchar.h>

// TODO: reference additional headers your program requires here
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <TlHelp32.h>
#include <Shlwapi.h>

#pragma comment(lib, "shlwapi")

#define CLIENT_PATH L"Rain.Client.dll"

#ifdef X86
#define BOOTSTRAPPER_PATH L"Rain.Bootstrapper.x86.dll"
#endif

#ifdef X64
#define BOOTSTRAPPER_PATH L"Rain.Bootstrapper.x64.dll"
#endif

using namespace std;

VOID Log(PCHAR text)
{
  printf(text);
  printf("\r\n");
}

VOID AcquireDebugPrivilege()
{
  Log("Acquiring debugger privilege...");
  Log("Getting current process...");
  auto process = GetCurrentProcess();
  if (process == NULL)
  {
    throw "Cannot open process!";
  }

  Log("Opening process token...");
  HANDLE token;
  auto isSucceeded = OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, &token);
  if (!isSucceeded)
  {
    throw "Could not acquire process token!";
  }

  Log("Adjusting process privileges...");
  TOKEN_PRIVILEGES privileges = { 0 };
  privileges.PrivilegeCount = 1;
  privileges.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;
  LookupPrivilegeValue(NULL, SE_DEBUG_NAME, &privileges.Privileges[0].Luid);

  isSucceeded = AdjustTokenPrivileges(token, FALSE, &privileges, sizeof(privileges), NULL, NULL);
  if (!isSucceeded)
  {
    throw "Could not adjust process privileges!";
  }

  CloseHandle(token);
}

LPVOID LoadArgument(HANDLE process, SIZE_T size, LPVOID data)
{
  Log("Allocating memory for argument...");
  auto baseAddress = VirtualAllocEx(process, NULL, size, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
  if (baseAddress == NULL)
  {
    throw "Could not allocate memory for argument!";
  }

  Log("Writing argument...");
  auto isSucceeded = WriteProcessMemory(process, baseAddress, data, size, NULL);
  if (!isSucceeded)
  {
    throw "Could not write memory for argument!";
  }

  return baseAddress;
}

VOID UnloadArgument(HANDLE process, LPVOID baseAddress)
{
  Log("Releasing memory allocated for argument...");
  auto isSucceeded = VirtualFreeEx(process, baseAddress, 0, MEM_RELEASE);
  if (!isSucceeded)
  {
    throw "Could release memory for argument!";
  }
}

inline size_t GetStringAllocSize(const TCHAR* str)
{
  return _tcsnlen(str, 65536) * sizeof(TCHAR) + sizeof(TCHAR);
}

VOID ExecuteRemoteFunction(HANDLE process, LPVOID function, TCHAR* argument = NULL)
{
  LPVOID baseAddress = 0;
  if (argument)
  {
    baseAddress = LoadArgument(process, GetStringAllocSize(argument), (void*)argument);
  }

  Log("Creating remote thread...");
  auto thread = CreateRemoteThread(process, NULL, 0, (LPTHREAD_START_ROUTINE)function, baseAddress, NULL, 0);
  if (thread == NULL)
  {
    throw "Could not create remote thread!";
  }

  Log("Waiting for action to finish...");
  WaitForSingleObject(thread, INFINITE);

  if (argument)
  {
    UnloadArgument(process, baseAddress);
  }
}

DWORD_PTR GetModuleHandle(INT processId, TCHAR* moduleName)
{
  Log("Getting module handle...");
  Log("Creating process snapshot...");
  MODULEENTRY32 moduleEntry32 = { 0 };
  moduleEntry32.dwSize = sizeof(MODULEENTRY32);
  
  auto snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE, processId);
  if (snapshot == INVALID_HANDLE_VALUE)
  {
    throw "Could not create snapshot!";
  }
  
  if (!Module32First(snapshot, &moduleEntry32))
  {
    throw "The module list is empty!";
  }

  while (_tcscmp(moduleEntry32.szModule, moduleName) != 0 && Module32Next(snapshot, &moduleEntry32));

  if (_tcscmp(moduleEntry32.szModule, moduleName) != 0)
  {
    throw "Could not find the specified module!";
  }
  Log("Module found.");

  Log("Closing snapshot...");
  auto isSucceeded = CloseHandle(snapshot);
  if (!isSucceeded)
  {
    throw "Could not close snapshot!";
  }

  return (DWORD_PTR)moduleEntry32.modBaseAddr;
}

DWORD_PTR GetFunctionOffset(TCHAR* libraryPath, CHAR* functionName)
{
  Log("Getting function offset...");
  Log("Loading library...");
  auto library = LoadLibrary(libraryPath);
  if (library == NULL)
  {
    throw "Could not load library!";
  }

  Log("Get address of function...");
  auto function = GetProcAddress(library, functionName);
  
  auto offset = (DWORD_PTR)function - (DWORD_PTR)library;

  Log("Unloading library...");
  auto isSucceeded = FreeLibrary(library);
  if (!isSucceeded)
  {
    throw "Could not unload library!";
  }

  return offset;
}

int _tmain(int argc, TCHAR* argv[])
{
  try
  {
    auto processId = _ttoi(argv[1]);

    TCHAR bootstrapperPath[MAX_PATH];
    GetModuleFileName(NULL, bootstrapperPath, MAX_PATH);
    PathRemoveFileSpec(bootstrapperPath);
    PathAppend(bootstrapperPath, BOOTSTRAPPER_PATH);

    TCHAR clientPath[MAX_PATH];
    GetModuleFileName(NULL, clientPath, MAX_PATH);
    PathRemoveFileSpec(clientPath);
    PathAppend(clientPath, CLIENT_PATH);

    AcquireDebugPrivilege();

    Log("Opening remote process...");
    auto process = OpenProcess(PROCESS_ALL_ACCESS, false, processId);
    if (!process)
    {
      throw "Cannot open process!";
    }
    
    Log("Injecting bootstrapper...");
    auto loadLibrary = GetProcAddress(GetModuleHandle(L"Kernel32"), "LoadLibraryW");
    ExecuteRemoteFunction(process, loadLibrary, bootstrapperPath);

    Log("Executing bootstrapper...");
    Log("Retrieving bootstrapper module handle...");
    auto module = GetModuleHandle(processId, BOOTSTRAPPER_PATH);
    Log("Retrieving bootstrapper function handle...");
    auto function = module + GetFunctionOffset(bootstrapperPath, "LoadDotNetLibrary");

    Log("Executing bootstrapper...");
    ExecuteRemoteFunction(process, (LPVOID)function, clientPath);

    /*Log("Unloading bootstrapper...");
    auto freeLibrary = GetProcAddress(GetModuleHandle(L"Kernel32"), "FreeLibrary");
    ExecuteRemoteFunction(process, freeLibrary);*/
  }
  catch (PCHAR error)
  {
    Log(error);
    Log("Aborting...");
    return 0;
  }

  Log("Success!");
  return 0;
}

