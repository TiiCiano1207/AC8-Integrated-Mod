-- Read the game's active camera components, independently of the F3 preference.
-- Never infer cockpit/external view from position, which our override can change.
local M={}
local function active(camera)
    return camera and camera:IsValid() and camera.bIsActive==true
end
function M.resolve(pawn)
    local ok,stock,name=pcall(function()
        local view=pawn.CameraViewComponent
        if not view or not view:IsValid() then return true,"Unknown (native camera retained)" end
        if active(view.CachedCockpitCamera) then return true,"Cockpit" end
        if active(view.CachedFirstPersonCamera) then return true,"HUD" end
        if active(view.CachedThirdPersonCamera) then return false,"ThirdPerson" end
        return true,"Transition (native camera retained)"
    end)
    if not ok then return true,"Unavailable (native camera retained)" end
    return stock,name
end
return M
