#pragma once
#include "runtime_settings.h"
runtime_settings::Store saved_runtime_settings;

// Called only by the existing background logger. Input callbacks never touch disk.
void flush_runtime_settings(){
    static uint64_t written_revision=0;
    auto data=saved_runtime_settings.snapshot();
    if(!data.revision||data.revision==written_revision)return;
    wchar_t target[MAX_PATH]{},temp[MAX_PATH]{};
    swprintf_s(target,L"%s\\runtime-settings.ini",module_folder);
    swprintf_s(temp,L"%s\\runtime-settings.ini.tmp",module_folder);
    const auto text=data.text();
    HANDLE file=CreateFileW(temp,GENERIC_WRITE,0,nullptr,CREATE_ALWAYS,FILE_ATTRIBUTE_NORMAL,nullptr);
    if(file==INVALID_HANDLE_VALUE)return;
    DWORD written=0;bool ok=WriteFile(file,text.data(),static_cast<DWORD>(text.size()),&written,nullptr)&&written==text.size();
    CloseHandle(file);
    if(ok&&MoveFileExW(temp,target,MOVEFILE_REPLACE_EXISTING|MOVEFILE_WRITE_THROUGH))written_revision=data.revision;
    else DeleteFileW(temp); // A failed write is retried on the next logger tick.
}
