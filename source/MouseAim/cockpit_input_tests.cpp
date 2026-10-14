#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <cstdint>
static bool key_c=false;
static HWND WINAPI input_foreground(){return reinterpret_cast<HWND>(uintptr_t(0x1238));}
static DWORD WINAPI input_process(HWND,LPDWORD p){*p=GetCurrentProcessId();return GetCurrentThreadId();}
static SHORT WINAPI input_key(int key){return key=='C'&&key_c?SHORT(0x8000):0;}
#define GetForegroundWindow input_foreground
#define GetWindowThreadProcessId input_process
#define GetAsyncKeyState input_key
#include "src/mouse_aim.cpp"
#include <cassert>
#include <cstdio>
static flight::V currentLook(){return flight::basis(look_pitch.load(),look_yaw.load(),0).f;}
int main(){
 QueryPerformanceFrequency(&perf_frequency);
 aircraft=65536;enabled=active=true;running=true;game_paused=gaze_active=context_suspended=false;
 cockpit_view_active=true;manual_camera_active=true;control_mode=0;live_sensitivity=.1f;
 for(bool independent:{false,true}){
  independent_mouse=independent;key_c=false;free_look.reset();mouse_delta.clear();
  recenter_requested=resume_center_requested=transition_center_requested=false;previous_pose_tick=0;
  pose_pitch=10;pose_yaw=30;pose_roll=70;target_pitch=-12;target_yaw=110;look_pitch=-12;look_yaw=110;
  auto angles=smooth_view::angles(smooth_view::mul(smooth_view::from({10,30,70}),smooth_view::from({5,25,0})));
  double pose[13]={65536,10,30,70,angles.p,angles.y,angles.r,80,120,7,180,0,0};
  receive_pose(pose);key_c=true;
  if(independent){target_input_tick();}else{receive_pose(pose);}
  assert(free_look.held&&free_look.relative);auto retained=free_look.local_direction;
  for(int tick=0;tick<120;++tick){
   pose[1]=std::sin(tick*.035)*65;pose[2]=std::sin(tick*.029)*170;pose[3]=std::sin(tick*.049)*170;
   // Deliberately retain the old rendered camera snapshot; the current body pose must still carry the head.
   receive_pose(pose);
   auto body=flight::basis(float(pose[1]),float(pose[2]),float(pose[3]));
   auto expected=flight::unit(body.f*retained.x+body.r*retained.y+body.u*retained.z);
   assert(flight::dot(currentLook(),expected)>.999999f);
   assert(std::abs(target_pitch.load()+12)<.001&&std::abs(target_yaw.load()-110)<.001);
  }
  mouse_delta.add(200,-100);
  if(independent)target_input_tick();else receive_pose(pose);
  assert(std::abs(flight::yaw(free_look.local_direction)-45)<.002&&std::abs(flight::pitch(free_look.local_direction)-15)<.002);
  assert(std::abs(target_pitch.load()+12)<.001&&std::abs(target_yaw.load()-110)<.001);
  key_c=false;mouse_delta.add(1000,1000);
  if(independent)target_input_tick();else receive_pose(pose);
  assert(std::abs(target_pitch.load()+12)<.001&&std::abs(target_yaw.load()-110)<.001);
  assert(flight::dot(currentLook(),flight::basis(-12,110,0).f)>.999999f);
 }
 puts("cockpit_input_tests PASS: both input paths, same-frame no-mouse body reprojection, local mouse axes and preserved world flight goal");
}
