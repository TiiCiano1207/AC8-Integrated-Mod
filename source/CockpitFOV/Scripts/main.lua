-- Independent cockpit, HUD and external-camera profiles.
-- Edit FOV.ini and restart to apply; 0 releases the game's original dynamic FOV.
local prefix = "[AC8FOV] "
local directory = assert(debug.getinfo(1, "S").source:sub(2):match("^(.*[/\\])"))
local file = assert(io.open(directory .. "../FOV.ini", "r"), "Cannot open FOV.ini")
local text = file:read("*a")
file:close()
local settings = {}
for line in text:gmatch("[^\r\n]+") do
    if not line:match("^%s*#") and not line:match("^%s*$") then
        local key, raw = line:match("^%s*(%a+)%s*=%s*(.-)%s*$")
        assert(key == "Cockpit" or key == "HUD" or key == "ThirdPerson", "Unknown FOV setting: " .. line)
        local value = tonumber(raw)
        assert(value and (value == 0 or (value >= 40 and value <= 130)), "Invalid FOV for " .. key)
        assert(settings[key] == nil, "Duplicate FOV setting: " .. key)
        settings[key] = value
    end
end
assert(settings.Cockpit and settings.HUD and settings.ThirdPerson, "FOV.ini requires Cockpit, HUD and ThirdPerson")

local views, managers = {}, {}
local failed = false
local function remember(list, object)
    if not object:IsValid() then return end
    for _, entry in ipairs(list) do
        if entry.object == object then return end
    end
    list[#list + 1] = {object = object}
end

for class, list in pairs({LiveCameraViewComponent = views, LivePlayerCameraManager = managers}) do
    NotifyOnNewObject("/Script/Live." .. class, function(object)
        ExecuteInGameThread(function() remember(list, object) end)
    end)
    ExecuteInGameThread(function()
        for _, object in ipairs(FindAllOf(class) or {}) do remember(list, object) end
    end)
end

local function active(camera)
    return camera:IsValid() and camera.bIsActive
end

local function currentMode(manager)
    local world, controller = manager:GetWorld(), manager.PCOwner
    if not world:IsValid() or world.NetDriver:IsValid() or not controller:IsValid() then return nil end
    local target = controller:GetViewTarget()
    for _, entry in ipairs(views) do
        local view = entry.object
        if view:IsValid() and view:GetOwner() == target then
            if active(view.CachedCockpitCamera) then return "Cockpit" end
            if active(view.CachedFirstPersonCamera) then return "HUD" end
            if active(view.CachedThirdPersonCamera) then return "ThirdPerson" end
        end
    end
end

-- This game's final FOV is controlled by PlayerController:FOV; changing a
-- cockpit component's FieldOfView alone does not change the rendered view.
LoopInGameThreadAfterFrames(1, function()
    if failed then return end
    local ok, err = pcall(function()
        for _, list in ipairs({views, managers}) do
            for i = #list, 1, -1 do
                if not list[i].object:IsValid() then table.remove(list, i) end
            end
        end
        for _, entry in ipairs(managers) do
            local manager = entry.object
            if manager.PCOwner:IsValid() then
                local mode = currentMode(manager)
                local desired = mode and settings[mode] or 0
                if entry.applied == nil or desired ~= entry.applied or
                    (desired > 0 and math.abs(manager:GetFOVAngle() - desired) > 0.01) then
                    -- FOV(0) unlocks the engine's normal camera/zoom behavior.
                    manager.PCOwner:FOV(desired)
                end
                if entry.mode ~= mode or entry.applied ~= desired then
                    print(string.format("%s%s: %s; output %.2f\n", prefix, mode or "Other",
                        desired == 0 and "original" or tostring(desired), manager:GetFOVAngle()))
                end
                entry.mode, entry.applied = mode, desired
            end
        end
    end)
    if not ok then
        failed = true
        for _, entry in ipairs(managers) do
            pcall(function()
                if entry.object:IsValid() and entry.object.PCOwner:IsValid() and (entry.applied or 0) > 0 then
                    entry.object.PCOwner:FOV(0)
                end
            end)
        end
        print(prefix .. "STOPPED: " .. tostring(err) .. "\n")
    end
end)
print(string.format("%sReady: Cockpit=%g, HUD=%g, ThirdPerson=%g\n",
    prefix, settings.Cockpit, settings.HUD, settings.ThirdPerson))
