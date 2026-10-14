#include "src/first_person_camera.h"
#include <cassert>
#include <cstdio>
using namespace smooth_view;
int main(){
 for(double p:{-89.9,-45.,0.,45.,89.9})for(double y:{-179.,-90.,0.,90.,179.})for(double r:{-175.,-30.,0.,30.,175.}){
  Q q=from({p,y,r});assert(distance(q,from(angles(q)))<.00001);
 }
 auto simulate=[](int fps,Angles start,Angles goal,double duration){Follow f;Angles view=f.step(1,2,start,goal,1./fps,6);assert(view.p==start.p&&view.y==start.y&&view.r==start.r);for(int i=0;i<int(fps*duration);++i)view=f.step(1,2,start,goal,1./fps,6);return view;};
 // No first-frame snap; bounded monotonic approach, smooth ease-in and settling.
 Follow f;Angles native{0,0,0},target{20,90,25};Angles a=f.step(1,2,native,target,1./120,6);
 double previous=distance(from(a),from(target)),firstMove=0,secondMove=0;
 for(int i=0;i<240;++i){Angles next=f.step(1,2,native,target,1./120,6);double move=distance(from(a),from(next));if(i==0)firstMove=move;if(i==1)secondMove=move;double error=distance(from(next),from(target));assert(error<=previous+.00001);assert(move<4);previous=error;a=next;}
 assert(firstMove<secondMove&&previous<.001);
 // The yaw seam takes the short route; almost vertical and 180-degree targets remain finite.
 auto seam=simulate(120,{0,179,0},{0,-179,0},.5);assert(distance(from(seam),from({0,-179,0}))<.04);
 for(Angles goal:{Angles{89.99,179,45},Angles{-89.99,-179,-45},Angles{0,180,0}}){auto v=simulate(120,{0,0,0},goal,2);assert(std::isfinite(v.p)&&std::isfinite(v.y)&&std::isfinite(v.r));assert(distance(from(v),from(goal))<.001);}
 // Same elapsed time gives the same response at low/high/variable frame rates.
 auto low=simulate(30,native,target,.5),high=simulate(144,native,target,.5);
 assert(distance(from(low),from(high))<.01);
 f.reset();a=f.step(1,2,native,target,.01,6);double elapsed=0;
 for(double dt:{.017,.009,.033,.011,.020,.041,.015,.028,.026,.050,.040,.030,.035,.045,.030,.070}){elapsed+=dt;a=f.step(1,2,native,target,dt,6);}
 auto uniform=simulate(1000,native,target,elapsed);assert(distance(from(a),from(uniform))<.02);
 // Pawn/manager changes, interruptions and hitches reseed native POV.
 Angles changed{30,-60,15};a=f.step(3,2,changed,target,.01,6);assert(a.p==changed.p&&a.y==changed.y&&a.r==changed.r);
 a=f.step(3,4,native,target,.01,6);assert(a.p==0&&a.y==0&&a.r==0);
 a=f.step(3,4,changed,target,.8,6);assert(a.p==changed.p&&a.y==changed.y&&a.r==changed.r);
 f.reset();a=f.step(3,4,native,target,.01,6);assert(a.p==0&&a.y==0&&a.r==0);
 // A moving target and reversal have no one-frame orientation jumps or divergent drift.
 double largest=0;for(int i=0;i<360;++i){Angles goal=i<180?Angles{25,110,15}:Angles{-20,-50,-15};Angles next=f.step(3,4,native,goal,1./120,6);largest=std::max(largest,distance(from(a),from(next)));a=next;}assert(largest<7);
 // Cockpit reference removes extra head roll at every look direction and bank.
 for(double bank:{-170.,-90.,-60.,0.,60.,90.,170.})for(double yaw:{-170.,-60.,0.,60.,170.}){
  Angles aircraft{25,35,bank},view{45,yaw,80};auto fixed=cockpit_angles(view,aircraft);
  auto relative=mul(conjugate(from(aircraft)),from(fixed));assert(std::abs(angles(relative).r)<.001);
  auto originalDirection=from({view.p,view.y,0}),correctedDirection=from({fixed.p,fixed.y,0});
  assert(distance(originalDirection,correctedDirection)<.00001);
 }
 puts("first_person_camera_tests PASS: quaternion continuity, smooth starts/stops, frame-rate consistency, resets and reversal");
}
