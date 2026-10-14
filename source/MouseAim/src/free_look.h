#pragma once
#include "flight_math.h"
namespace flight {
struct FreeLook {
    bool held=false,relative=false;
    V direction{1,0,0},local_direction{1,0,0};
    void reset() { held=relative=false; }
    V with_body(const Basis& body) const {return unit(body.f*local_direction.x+body.r*local_direction.y+body.u*local_direction.z);}
    V step(bool down, V& aim, const Basis& view, float dx, float dy,float fov=62,float reference_fov=62,V offset={},const Basis* body=nullptr) {
        // Discard boundary-frame deltas so release cannot move the flight target.
        if (down!=held || (down&&relative!=(body!=nullptr))) {
            held=down;
            relative=body!=nullptr;
            if (held){
                direction=view.f;
                if(relative)local_direction=unit({dot(direction,body->f),dot(direction,body->r),dot(direction,body->u)});
            }
            dx=dy=0;
        }
        if (held && relative) {
            const float p=std::clamp(pitch(local_direction)-dy,-89.0f,89.0f);
            local_direction=basis(p,yaw(local_direction)+dx,0).f;
            direction=with_body(*body);return direction;
        }
        if (held) {
            // World-up yaw and bounded pitch give an orbit without pole flips.
            const float p=std::clamp(pitch(direction)-dy,-89.0f,89.0f);
            direction=basis(p,yaw(direction)+dx,0).f;
            return direction;
        }
        aim=move_world_target(aim,view,dx,dy,fov,reference_fov,offset);
        return aim;
    }
};
}
