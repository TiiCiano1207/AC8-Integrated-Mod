#include "src/free_look.h"
#include "src/first_person_camera.h"
#include <cassert>
#include <cstdio>
using namespace flight;
static float difference(V a,V b){return std::sqrt(dot(a-b,a-b));}
static V world(const Basis& b,V local){return unit(b.f*local.x+b.r*local.y+b.u*local.z);}
static smooth_view::Angles composed(smooth_view::Angles body,smooth_view::Angles head){return smooth_view::angles(smooth_view::mul(smooth_view::from(body),smooth_view::from(head)));}
int main(){
 V aim=basis(-12,110,0).f,original=aim;FreeLook look;
 auto body=basis(20,30,70);auto viewAngles=composed({20,30,70},{8,25,0});auto view=basis(float(viewAngles.p),float(viewAngles.y),float(viewAngles.r));
 auto first=look.step(true,aim,view,99,99,80,62,{},&body);
 assert(difference(first,view.f)<.00001f&&difference(aim,original)<.00001f);
 auto local=look.local_direction;
 // With no mouse movement the held head offset moves with pitch/yaw/roll, including inverted flight.
 for(float p:{-65.f,0.f,65.f})for(float y:{-175.f,0.f,175.f})for(float r:{-170.f,-90.f,0.f,90.f,170.f}){
  body=basis(p,y,r);auto next=look.step(true,aim,view,0,0,80,62,{},&body);
  assert(difference(next,world(body,local))<.00001f&&difference(aim,original)<.00001f);
 }
 body=basis(15,80,90);look.reset();view=body;look.step(true,aim,view,0,0,80,62,{},&body);
 auto right=look.step(true,aim,view,20,0,80,62,{},&body);
 assert(std::abs(dot(right,body.r)-std::sin(20*rad))<.00001f&&std::abs(dot(right,body.u))<.00001f);
 auto up=look.step(true,aim,view,0,-10,80,62,{},&body);
 assert(std::abs(dot(up,body.u)-std::sin(10*rad))<.00001f&&difference(aim,original)<.00001f);
 // Release discards its boundary delta and restores the same world flight goal.
 auto released=look.step(false,aim,view,1000,1000,80,62,{},&body);
 assert(difference(released,original)<.00001f&&difference(aim,original)<.00001f);
 FreeLook external,internal;V a=original,b=original;
 external.step(false,a,view,30,-15,80,62,{});internal.step(false,b,view,30,-15,80,62,{},&body);
 assert(difference(a,b)<.00001f); // Ordinary flight-target input is unchanged.
 // Changing view type while C is held rebases the head instead of reusing the wrong coordinates.
 look.reset();look.step(true,aim,view,0,0);look.step(true,aim,view,12,-7);
 look.step(true,aim,view,500,500,80,62,{},&body);assert(difference(look.direction,view.f)<.00001f);
 // Local smoothing carries body movement directly: a steady relative look never lags aircraft motion.
 smooth_view::CockpitFollow camera; smooth_view::Angles aircraft{20,30,70},head{10,25,0};
 auto native=composed(aircraft,head);auto output=camera.step(1,2,native,native,1./120,6,aircraft,true);
 for(int tick=0;tick<480;++tick){
  aircraft={std::sin(tick*.013)*65,std::sin(tick*.019)*175,std::sin(tick*.022)*170};
  auto wanted=composed(aircraft,head);output=camera.step(1,2,wanted,wanted,1./120,6,aircraft,true);
  auto actual=smooth_view::CockpitFollow::relative(output,aircraft);
  assert(smooth_view::distance(smooth_view::from(actual),smooth_view::from(head))<.0001);
 }
 auto before=output;auto flightGoal=smooth_view::cockpit_angles({-30,120,0},aircraft);
 output=camera.step(1,2,composed(aircraft,{0,0,0}),flightGoal,1./120,6,aircraft,false);
 assert(smooth_view::distance(smooth_view::from(before),smooth_view::from(output))<.0001);
 for(int tick=0;tick<240;++tick)output=camera.step(1,2,native,flightGoal,1./120,6,aircraft,false);
 assert(smooth_view::distance(smooth_view::from(output),smooth_view::from(flightGoal))<.001);
 camera.reset();assert(!camera.active&&!camera.head.active&&!camera.world.active);
 puts("cockpit_local_tests PASS: body-relative axes, no-mouse maneuver carry, inversion, goal retention, scope changes, local smoothing and smooth return");
}
