#pragma once
#include <array>
#include <cmath>
#include <cstdint>
#include <mutex>
#include <sstream>
#include <iomanip>
#include <locale>

namespace runtime_settings {
enum Key { Sensitivity, Zoom, ControlMode, CameraMode, Boresight, Count };
struct Entry { double value=0; uint64_t revision=0; };
struct Snapshot {
    uint64_t revision=0;
    std::array<Entry,Count> entries{};
    std::string text() const {
        static const char* names[]={"sensitivity","free_look_zoom","control_mode","camera_mode","hud_boresight"};
        std::ostringstream out;out.imbue(std::locale::classic());out<<std::setprecision(8);
        out<<"version=1\nrevision="<<revision<<"\n";
        for(int i=0;i<Count;++i)if(entries[i].revision){
            out<<names[i]<<"="<<entries[i].value<<"\n";
            out<<names[i]<<"_revision="<<entries[i].revision<<"\n";
        }
        return out.str();
    }
};
class Store {
    std::mutex mutex;
    Snapshot state;
public:
    bool record(Key key,double value){
        if(key<0||key>=Count||!std::isfinite(value))return false;
        if(key==Sensitivity)value=std::round(value*100000)/100000;
        if(key==Sensitivity&&(value<.01||value>1))return false;
        if(key==Zoom&&(value<1||value>3))return false;
        if(key==ControlMode&&value!=0&&value!=3)return false;
        if(key==CameraMode&&value!=0&&value!=1&&value!=2)return false;
        if(key==Boresight&&value!=0&&value!=1)return false;
        std::lock_guard<std::mutex> lock(mutex);
        state.entries[key]={value,++state.revision};return true;
    }
    Snapshot snapshot(){std::lock_guard<std::mutex> lock(mutex);return state;}
};
}
