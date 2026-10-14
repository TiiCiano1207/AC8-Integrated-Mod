#include "src/first_person_camera.h"
#include <cassert>
#include <cstdio>
using namespace smooth_view;
int main(){
 // Reproduce the prior hard flip with a fixed backward look, then check physical orientation continuity.
 for(bool horizontal:{false,true}){
  RollReturn recovery;Angles last;bool have=false;double largest=0;
  for(int tick=0;tick<=720;++tick){
   double a=tick*.25;Angles body=horizontal?Angles{0,a,90}:Angles{a,0,0};
   auto wanted=cockpit_angles({0,180,0},body);auto result=recovery.step(wanted,body,1./120,6);
   assert(result.p==wanted.p&&result.y==wanted.y); // Look direction is not modified.
   if(have)largest=std::max(largest,distance(from(last),from(result)));
   assert(std::abs(recovery.velocity)<=120.0001);last=result;have=true;
  }
  assert(largest<2); // Former projection changed orientation 180 degrees in one frame.
 }
 // A half-turn correction is bounded and eases to the target instead of snapping.
 RollReturn r;Angles body{0,0,0},start{0,0,0},goal{0,0,180};r.seed(start,body);
 Angles last=start,result=start;double first=0,largest=0;
 for(int i=0;i<720;++i){result=r.step(goal,body,1./120,6);double movement=distance(from(last),from(result));if(i==0)first=movement;largest=std::max(largest,movement);last=result;}
 assert(first<.03&&largest<1.01&&distance(from(result),from(goal))<.001);
 // Stable half-turn selection under tiny sign fluctuations.
 r.reset();r.seed(start,body);double previousError=180;
 for(int i=0;i<120;++i){auto v=r.step({0,0,i%2?179.999:-179.999},body,1./120,6);double error=distance(from(v),from(goal));assert(error<=previousError+.003);previousError=error;}
 // Real aircraft roll is carried directly; only the camera's corrective twist is limited.
 r.reset();r.seed(start,body);
 for(int i=0;i<120;++i){Angles plane{0,0,i*1.5};auto v=r.step(plane,plane,1./120,6);assert(distance(from(v),from(plane))<.0001);}
 auto simulate=[](int fps){RollReturn f;f.seed({0,0,0},{0,0,0});Angles v;for(int i=0;i<fps;++i)v=f.step({0,0,180},{0,0,0},1./fps,6);return v;};
 assert(distance(from(simulate(30)),from(simulate(144)))<.3);
 puts("camera_return_tests PASS: pole-crossing continuity, angle-based speed, half-turn stability, aircraft carry and frame-rate consistency");
}
