#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <cstdio>
#include <cassert>
#include <fstream>
#include <iostream>
#include <filesystem>
wchar_t module_folder[MAX_PATH]{};
#include "src/runtime_settings_io.h"
int main(){
    wchar_t temp[MAX_PATH]{};GetTempPathW(MAX_PATH,temp);
    std::filesystem::path folder=std::filesystem::path(temp)/(L"AC8-runtime-settings-test-"+std::to_wstring(GetCurrentProcessId()));
    std::filesystem::create_directory(folder);
    wcscpy_s(module_folder,(folder/L"not-created").c_str());
    saved_runtime_settings.record(runtime_settings::Sensitivity,.01f);flush_runtime_settings();
    wcscpy_s(module_folder,folder.c_str());flush_runtime_settings();
    auto read=[&]{std::ifstream file(folder/L"runtime-settings.ini");return std::string(std::istreambuf_iterator<char>(file),{});};
    assert(read().find("sensitivity=0.01\n")!=std::string::npos);
    saved_runtime_settings.record(runtime_settings::ControlMode,3);flush_runtime_settings();
    assert(read().find("control_mode=3\n")!=std::string::npos);
    assert(read().find("revision=2\n")!=std::string::npos);
    assert(!std::filesystem::exists(folder/L"runtime-settings.ini.tmp"));
    // Only remove the three explicitly named test items within this unique folder.
    std::filesystem::remove(folder/L"runtime-settings.ini");std::filesystem::remove(folder);
    std::cout<<"PASS Windows atomic runtime writes, minimum float sensitivity and retry after write failure\n";
}
