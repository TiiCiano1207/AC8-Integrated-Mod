#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <cstdint>
static HWND WINAPI fp_foreground(){return reinterpret_cast<HWND>(uintptr_t(0x1238));}
static DWORD WINAPI fp_process(HWND,LPDWORD p){*p=GetCurrentProcessId();return GetCurrentThreadId();}
static SHORT WINAPI fp_key(int){return 0;}
#define GetForegroundWindow fp_foreground
#define GetWindowThreadProcessId fp_process
#define GetAsyncKeyState fp_key
#include "src/mouse_aim.cpp"
#include <cassert>
#include <cstdio>
int main(){
 QueryPerformanceFrequency(&perf_frequency);
 alignas(8) std::array<unsigned char,0x300> pawn{},root{};
 alignas(8) std::array<unsigned char,0x1600> manager{};manager.fill(0x5a);
 *reinterpret_cast<uintptr_t*>(manager.data())=reinterpret_cast<uintptr_t>(GetModuleHandleW(nullptr))+0xC9D3160;
 *reinterpret_cast<uintptr_t*>(pawn.data()+0x1A0)=reinterpret_cast<uintptr_t>(root.data());
 double position[]={1000000,2000000,3000000};memcpy(root.data()+0x220,position,24);
 double native[]={1000120,2000007,3000180,12,77,-3};memcpy(manager.data()+0x14A0,native,48);
 float fov=120;memcpy(manager.data()+0x14D0,&fov,4);auto nativeBytes=manager;
 CameraCommand cmd;cmd.pawn=reinterpret_cast<uintptr_t>(pawn.data());cmd.manager=reinterpret_cast<uintptr_t>(manager.data());cmd.p=-25;cmd.y=140;cmd.r=20;cmd.stock_view=true;cmd.follow_first_person=true;
 CameraSample sample;first_person_camera_enabled=true;first_person_camera_response=6;
 for(int preference:{0,1,2}){
  first_person_camera.reset();manager=nativeBytes;assert(apply_native_camera(manager.data(),cmd,preference,sample,1.f/120));
  assert(std::abs(sample.pov[3]-native[3])<.0001&&std::abs(sample.pov[4]-native[4])<.0001&&std::abs(sample.pov[5])<.0001);
  for(int frame=0;frame<240;++frame){manager=nativeBytes;assert(apply_native_camera(manager.data(),cmd,preference,sample,1.f/120));
   for(size_t i=0;i<manager.size();++i)if(i<0x14B8||i>=0x14D0)assert(manager[i]==nativeBytes[i]);
   assert(sample.fov==fov&&sample.pov[0]==native[0]&&sample.pov[1]==native[1]&&sample.pov[2]==native[2]);
  }
  auto ray=flight::unit(flight::basis(float(cmd.p),float(cmd.y),0).f*50000-flight::V{120,7,180});
  assert(smooth_view::distance(smooth_view::from({sample.pov[3],sample.pov[4],sample.pov[5]}),smooth_view::from(smooth_view::cockpit_angles({flight::pitch(ray),flight::yaw(ray),0},{0,0,0})))<.001);
 }
 // Reproduce C during maneuvers, release, and repeated look/return cycles.
 // Deliberately poison command roll with the last rendered roll: it must not accumulate.
 for(int cycle=0;cycle<4;++cycle){
  for(int tick=0;tick<240;++tick){
   pose_pitch=20;pose_yaw=35;pose_roll=std::sin(tick*.025f)*75;
   manual_camera_active=tick<120;cmd.p=tick<120?65:20;cmd.y=tick<120?-60:35;cmd.r=sample.pov[5];
   manager=nativeBytes;assert(apply_native_camera(manager.data(),cmd,1,sample,1.f/120));
   auto relative=smooth_view::mul(smooth_view::conjugate(smooth_view::from({pose_pitch.load(),pose_yaw.load(),pose_roll.load()})),smooth_view::from({sample.pov[3],sample.pov[4],sample.pov[5]}));
   assert(std::isfinite(smooth_view::angles(relative).r));
   assert(std::abs(first_person_camera.upright.velocity)<=smooth_view::RollReturn::maximum_speed+.001);
   for(size_t i=0;i<manager.size();++i)if(i<0x14B8||i>=0x14D0)assert(manager[i]==nativeBytes[i]);
  }
 }
 pose_pitch=pose_yaw=pose_roll=0;manual_camera_active=false;cmd.p=-25;cmd.y=140;cmd.r=20;
 // Turning follow off or leaving the followed view restores native POV and clears the filter.
 first_person_camera_enabled=false;manager=nativeBytes;assert(apply_native_camera(manager.data(),cmd,1,sample));assert(manager==nativeBytes&&!first_person_camera.active);
 first_person_camera_enabled=true;cmd.follow_first_person=false;manager=nativeBytes;assert(apply_native_camera(manager.data(),cmd,1,sample));assert(manager==nativeBytes&&!first_person_camera.active);
 cmd.stock_view=false;assert(apply_native_camera(manager.data(),cmd,1,sample));assert(sample.pov[0]!=native[0]&&!first_person_camera.active);
 // Final HUD snapshot uses filtered orientation and the unchanged native location/FOV.
 running=active=enabled=true;context_suspended=game_paused=gaze_active=false;aircraft=cmd.pawn;pose_tick=GetTickCount64();camera_view_mode=1;
 original_camera_update=+[](void*,float){};manual_camera_active=false;
 HudFrame frame;frame.pawn=cmd.pawn;frame.tick=GetTickCount64();stage_hud_frame(frame);
 receive_camera(cmd.manager,cmd.pawn,cmd.p,cmd.y,cmd.r,true,true);manager=nativeBytes;update_native_camera(manager.data(),1.f/120);
 manager=nativeBytes;update_native_camera(manager.data(),1.f/120);HudFrame shown;assert(read_hud_frame(shown));
 assert(shown.ox==120&&shown.oy==7&&shown.oz==180&&shown.fov==fov);
 assert(std::abs(shown.cp-*reinterpret_cast<double*>(manager.data()+0x14B8))<.0001&&shown.cp!=12);
 // Pause/overridden focus/timeout disable writes and force a native seed on resumption.
 for(int gate=0;gate<4;++gate){
  manager=nativeBytes;game_paused=gate==0;gaze_active=gate==1;context_suspended=gate==2;
  if(gate==3){std::lock_guard<std::mutex> lock(camera_command_mutex);camera_command.tick=GetTickCount64()-300;}
  update_native_camera(manager.data(),1.f/120);assert(manager==nativeBytes&&!first_person_camera.active);
  game_paused=gaze_active=context_suspended=false;pose_tick=GetTickCount64();receive_camera(cmd.manager,cmd.pawn,cmd.p,cmd.y,cmd.r,true,true);
  update_native_camera(manager.data(),1.f/120);assert(first_person_camera.active);
  assert(std::abs(*reinterpret_cast<double*>(manager.data()+0x14B8)-native[3])<.0001);
  for(size_t i=0;i<manager.size();++i)if(i<0x14B8||i>=0x14D0)assert(manager[i]==nativeBytes[i]);
 }
 puts("first_person_context_tests PASS: rotation-only writes, modes, settings toggle, cockpit protection, final HUD and interruption reset");
}
