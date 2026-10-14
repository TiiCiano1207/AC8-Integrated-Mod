#include "src/runtime_settings.h"
#include <cassert>
#include <iostream>
#include <thread>
int main(){
    using namespace runtime_settings;
    Store store;assert(store.snapshot().revision==0);
    assert(!store.record(Sensitivity,0));assert(!store.record(CameraMode,3));assert(!store.record(ControlMode,1));
    assert(store.record(Sensitivity,.12345));assert(store.record(Zoom,2));
    auto state=store.snapshot();assert(state.revision==2&&state.entries[Sensitivity].revision==1);
    assert(state.text().find("sensitivity=0.12345\n")!=std::string::npos);
    assert(state.text().find("camera_mode=")==std::string::npos); // untouched fields must not overwrite menu edits
    assert(store.record(Sensitivity,.12345));assert(store.snapshot().entries[Sensitivity].revision==3); // explicit same-value adjustment still wins
    std::thread a([&]{for(int i=0;i<1000;++i)store.record(ControlMode,i%2?3:0);});
    std::thread b([&]{for(int i=0;i<1000;++i)store.record(Boresight,i%2);});
    a.join();b.join();assert(store.snapshot().revision==2003);
    std::cout<<"PASS runtime setting revisions, validation, sparse persistence and concurrent updates\n";
}
