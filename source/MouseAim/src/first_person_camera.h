#pragma once
#include <cmath>
#include <algorithm>
#include <cstdint>
namespace smooth_view {
constexpr double pi=3.14159265358979323846, rad=pi/180;
struct Angles {double p=0,y=0,r=0;};
struct Q {double w=1,x=0,y=0,z=0;};
inline double dot(Q a,Q b){return a.w*b.w+a.x*b.x+a.y*b.y+a.z*b.z;}
inline Q unit(Q q){double n=std::sqrt(dot(q,q));return {q.w/n,q.x/n,q.y/n,q.z/n};}
inline Q mul(Q a,Q b){return {a.w*b.w-a.x*b.x-a.y*b.y-a.z*b.z,a.w*b.x+a.x*b.w+a.y*b.z-a.z*b.y,a.w*b.y-a.x*b.z+a.y*b.w+a.z*b.x,a.w*b.z+a.x*b.y-a.y*b.x+a.z*b.w};}
inline Q from(Angles a){
 double p=-a.p*rad/2,y=a.y*rad/2,r=-a.r*rad/2;
 return unit(mul(mul({std::cos(y),0,0,std::sin(y)},{std::cos(p),0,std::sin(p),0}),{std::cos(r),std::sin(r),0,0}));
}
inline Q mix(Q a,Q b,double t){
 double d=dot(a,b);if(d<0){b={-b.w,-b.x,-b.y,-b.z};d=-d;}
 d=std::clamp(d,-1.,1.);double s=1-t,k=t;
 if(d<.9995){double angle=std::acos(d),denom=std::sin(angle);s=std::sin((1-t)*angle)/denom;k=std::sin(t*angle)/denom;}
 return unit({s*a.w+k*b.w,s*a.x+k*b.x,s*a.y+k*b.y,s*a.z+k*b.z});
}
inline Angles angles(Q q){
 // Matrix columns are Unreal forward/right/up, with the game's pitch/roll signs.
 double fx=1-2*(q.y*q.y+q.z*q.z),fy=2*(q.x*q.y+q.w*q.z),fz=2*(q.x*q.z-q.w*q.y);
 double rx=2*(q.x*q.y-q.w*q.z),ry=1-2*(q.x*q.x+q.z*q.z),rz=2*(q.y*q.z+q.w*q.x);
 double p=std::asin(std::clamp(fz,-1.,1.)),y=std::atan2(fy,fx);
 double rightDot=-rx*std::sin(y)+ry*std::cos(y);
 double upDot=-rx*std::sin(p)*std::cos(y)-ry*std::sin(p)*std::sin(y)+rz*std::cos(p);
 return {p/rad,y/rad,std::atan2(-upDot,rightDot)/rad};
}
inline Q conjugate(Q q){return {q.w,-q.x,-q.y,-q.z};}
inline Angles cockpit_angles(Angles view,Angles aircraft){
 // Head yaw/pitch are relative to the airframe, with zero head roll.
 // This preserves aircraft bank instead of flattening the world horizon.
 Q body=from(aircraft),direction=from({view.p,view.y,0});
 Q local=mul(conjugate(body),direction);
 auto head=angles(local);
 return angles(mul(body,from({head.p,head.y,0})));
}
inline double distance(Q a,Q b){return 2*std::acos(std::clamp(std::abs(dot(a,b)),0.,1.))/rad;}
// Two spherical low-pass stages soften acceleration and deceleration.
// Substeps + midpoint evaluation keep response stable across frame rates.
struct Follow {
 bool active=false;uintptr_t manager=0,pawn=0;Q first,second;
 void reset(){active=false;manager=pawn=0;}
 Angles step(uintptr_t new_manager,uintptr_t new_pawn,Angles native,Angles target,double dt,double response){
  if(!active||manager!=new_manager||pawn!=new_pawn||!std::isfinite(dt)||dt<=0||dt>.25){
   first=second=from(native);active=std::isfinite(dt)&&dt>0&&dt<=.25;manager=new_manager;pawn=new_pawn;
   return native; // Enter/recover from the game's actual POV, without a jump.
  }
  Q goal=from(target);int count=std::max(1,int(std::ceil(dt*480)));
  double h=dt/count,rate=2*std::clamp(response,2.,20.);
  double a=-std::expm1(-rate*h),half=-std::expm1(-rate*h/2);
  for(int i=0;i<count;++i){Q midpoint=mix(first,goal,half);second=mix(second,midpoint,a);first=mix(first,goal,a);}
  return angles(second);
 }
};
// Final camera-up recovery. Aircraft/control state is read-only.
struct D {double x=0,y=0,z=0;D operator+(D b)const{return {x+b.x,y+b.y,z+b.z};}D operator-(D b)const{return {x-b.x,y-b.y,z-b.z};}D operator*(double k)const{return {x*k,y*k,z*k};}};
inline double ddot(D a,D b){return a.x*b.x+a.y*b.y+a.z*b.z;}
inline D dcross(D a,D b){return {a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x};}
inline D dunit(D v){double n=std::sqrt(ddot(v,v));return v*(1/std::max(n,1e-12));}
inline D qrotate(Q q,D v){D xyz{q.x,q.y,q.z};auto t=dcross(xyz,v)*2;return v+t*q.w+dcross(xyz,t);}
inline D axis_rotate(D v,D axis,double a){return v*std::cos(a)+dcross(axis,v)*std::sin(a)+axis*(ddot(axis,v)*(1-std::cos(a)));}
struct RollReturn {
 bool active=false;D forward,up;Q body;double velocity=0,preferred=1;
 static constexpr double maximum_speed=120,acceleration=360;
 void reset(){active=false;velocity=0;preferred=1;}
 void seed(Angles view,Angles aircraft){auto q=from(view);forward=qrotate(q,{1,0,0});up=qrotate(q,{0,0,1});body=from(aircraft);active=true;velocity=0;}
 Angles step(Angles wanted,Angles aircraft,double dt,double response){
  if(!active||!std::isfinite(dt)||dt<=0||dt>.25){seed(wanted,aircraft);return wanted;}
  auto currentBody=from(aircraft),delta=mul(currentBody,conjugate(body));
  auto carriedForward=qrotate(delta,forward),carriedUp=qrotate(delta,up);
  auto goal=from(wanted);auto f=qrotate(goal,{1,0,0}),desiredUp=qrotate(goal,{0,0,1});
  // Carry actual aircraft motion, then parallel-transport to the unchanged look direction.
  auto axis=dcross(carriedForward,f);double sine=std::sqrt(ddot(axis,axis)),cosine=std::clamp(ddot(carriedForward,f),-1.,1.);
  if(sine>1e-9)carriedUp=axis_rotate(carriedUp,axis*(1/sine),std::atan2(sine,cosine));
  else if(cosine<0)carriedUp=axis_rotate(carriedUp,dunit(carriedUp),pi);
  auto u=dunit(carriedUp-f*ddot(carriedUp,f));
  int count=std::max(1,int(std::ceil(dt*240)));double h=dt/count;
  for(int i=0;i<count;++i){
   double error=std::atan2(ddot(f,dcross(u,desiredUp)),std::clamp(ddot(u,desiredUp),-1.,1.))/rad;
   // Retain the return direction near an ambiguous half-turn; do not alternate sides.
   if(std::abs(error)>175&&error*preferred<0)error=preferred*(360-std::abs(error));
   if(std::abs(error)>1&&std::abs(error)<175)preferred=std::copysign(1.,error);
   double speed=std::min({maximum_speed,std::clamp(response,2.,20.)*std::abs(error),std::sqrt(2*acceleration*std::abs(error))});
   double wantedVelocity=std::copysign(speed,error);
   velocity+=std::clamp(wantedVelocity-velocity,-acceleration*h,acceleration*h);
   double step=velocity*h;
   if(step*error>=0&&std::abs(step)>=std::abs(error)){step=error;velocity=0;}
   u=dunit(axis_rotate(u,f,step*rad));
  }
  forward=f;up=u;body=currentBody;
  double p=wanted.p*rad,y=wanted.y*rad;
  D right{-std::sin(y),std::cos(y),0},neutralUp{-std::sin(p)*std::cos(y),-std::sin(p)*std::sin(y),std::cos(p)};
  return {wanted.p,wanted.y,std::atan2(ddot(u,right),ddot(u,neutralUp))/rad};
 }
};
// Smooth only the head offset during C; apply aircraft motion directly.
struct CockpitFollow {
 bool active=false,previous_looking=false;uintptr_t manager=0,pawn=0;Angles last;
 Follow world,head;RollReturn upright;
 void reset(){active=previous_looking=false;manager=pawn=0;world.reset();head.reset();upright.reset();}
 static Angles relative(Angles view,Angles aircraft){auto a=angles(mul(conjugate(from(aircraft)),from(view)));return {a.p,a.y,0};}
 Angles step(uintptr_t new_manager,uintptr_t new_pawn,Angles native,Angles target,double dt,double response,Angles aircraft,bool looking){
  bool valid=std::isfinite(dt)&&dt>0&&dt<=.25;
  if(!active||manager!=new_manager||pawn!=new_pawn||!valid){reset();manager=new_manager;pawn=new_pawn;last=cockpit_angles(native,aircraft);previous_looking=looking;}
  if(looking!=previous_looking){world.reset();head.reset();}
  Angles result;
  if(looking){
   auto local=head.step(new_manager,new_pawn,relative(last,aircraft),relative(target,aircraft),dt,response);
   result=angles(mul(from(aircraft),from({local.p,local.y,0})));
   upright.seed(result,aircraft);
  }else{
   result=upright.step(cockpit_angles(world.step(new_manager,new_pawn,last,target,dt,response),aircraft),aircraft,dt,response);
  }
  last=result;previous_looking=looking;active=valid;return result;
 }
};

}
