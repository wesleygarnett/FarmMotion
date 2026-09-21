-- FarmMotion telemetry, adapted from Mhytee/Trueforce-For-All v0.2.6.
-- Copyright (c) 2026 Mhytee and FarmMotion contributors. GPL-2.0-only.
-- Changes: motion-only payload, independent pipe, instance/timing fields,
-- GUI/pause gating, chassis vertical velocity and body pitch/roll velocity.
FarmMotionTelemetry = {}
local ctx = { file=nil, elapsed=0, pending=0, retry=1000, seq=0,
    session=tostring({}), pipeName="\\\\.\\pipe\\FarmMotionTelemetry" }

local function finite(v)
    return type(v) == "number" and v == v and v > -math.huge and v < math.huge
end
local function quote(s)
    s = tostring(s or "")
    return '"' .. s:gsub('\\', '\\\\'):gsub('"', '\\"'):gsub('[%z\1-\31]',
        function(c) return string.format('\\u%04x', string.byte(c)) end) .. '"'
end
local function number(parts, key, v)
    if finite(v) then table.insert(parts, '"' .. key .. '":' .. string.format("%.6f", v)) end
end
local function field(w, key)
    local v = w[key]
    if v == nil and w.physics ~= nil then v = w.physics[key] end
    return v
end
local function currentVehicle()
    if g_currentMission == nil then return nil end
    if g_minModDescVersion ~= nil and g_minModDescVersion >= 90 then
        local ps = g_currentMission.playerSystem
        local p = ps and ps.playersByUserId[g_currentMission.playerUserId]
        if p ~= nil then return p.getCurrentVehicle() end
        return nil
    end
    return g_currentMission.controlledVehicle
end
local function surfaceInfo(w, entry)
    local p=w.physics
    if p == nil then return end
    if WheelsUtil ~= nil and p.tireType ~= nil then
        table.insert(entry,'"tire":'..quote(WheelsUtil.getTireTypeName(p.tireType)))
    end
    local ni=p.netInfo
    if ni ~= nil then number(entry,"omega",ni.xDriveSpeed) end -- radians/second
    if p.hasGroundContact ~= true or p.hasSnowContact == true or p.hasWaterContact == true then return end
    if WheelsUtil == nil or FieldGroundType == nil or WheelContactType == nil then return end
    if p.densityType == nil or p.contact == nil or not finite(p.groundDepth) then return end
    -- Same broad ground classes used by FS25 wheel friction. Object contact is
    -- road-like in this model; this is not a guarantee of actual asphalt material.
    local ground=WheelsUtil.getGroundType(p.densityType ~= FieldGroundType.NONE,
        p.contact ~= WheelContactType.GROUND,p.groundDepth)
    local name="unknown"
    if ground == WheelsUtil.GROUND_ROAD then name="road"
    elseif ground == WheelsUtil.GROUND_FIELD then name="field"
    elseif ground == WheelsUtil.GROUND_HARD_TERRAIN then name="hard"
    elseif ground == WheelsUtil.GROUND_SOFT_TERRAIN then name="soft" end
    table.insert(entry,'"surface":'..quote(name))
end
function FarmMotionTelemetry:buildLine()
    local parts = { '"v":1', '"surfaceVersion":1', '"session":'..quote(ctx.session),
        '"seq":'..ctx.seq, '"time":'..string.format("%.6f",ctx.elapsed/1000) }
    local ok, vehicle = pcall(currentVehicle)
    local paused = g_currentMission == nil or g_currentMission.isPaused == true
    if g_gui ~= nil and g_gui.getIsGuiVisible ~= nil then
        local o, visible = pcall(g_gui.getIsGuiVisible, g_gui)
        if o and visible then paused = true end
    end
    if not ok or vehicle == nil or paused then
        table.insert(parts, '"active":false')
        return "{" .. table.concat(parts,",") .. "}"
    end
    table.insert(parts, '"active":true')
    table.insert(parts, '"vehicle":'..quote(vehicle.rootNode))
    if vehicle.getLastSpeed ~= nil then
        local speedOk,speed=pcall(vehicle.getLastSpeed,vehicle)
        if speedOk and finite(speed) then number(parts,"speed",math.abs(speed)/3.6) end
    end
    local node = vehicle.components and vehicle.components[1] and vehicle.components[1].node
    if node ~= nil then
        if getLinearVelocity ~= nil then
            local o,x,y,z = pcall(getLinearVelocity,node)
            if o then number(parts,"vy",y) end
        end
        if getAngularVelocity ~= nil and worldDirectionToLocal ~= nil then
            local o,x,y,z = pcall(getAngularVelocity,node)
            if o and finite(x) and finite(y) and finite(z) then
                local o2,lx,ly,lz = pcall(worldDirectionToLocal,node,x,y,z)
                if o2 then number(parts,"pitchRate",lx); number(parts,"rollRate",lz) end
            end
        end
    end
    local wheels={}
    local spec=vehicle.spec_wheels
    if spec ~= nil and spec.wheels ~= nil then
        for i,w in ipairs(spec.wheels) do
            if i > 16 then break end
            local ni=w.netInfo or (w.physics and w.physics.netInfo)
            if w.physics ~= nil or ni ~= nil then
                local entry={ '"i":'..i }
                if ni ~= nil then number(entry,"y",ni.y) end
                local contact=field(w,"hasGroundContact")
                if contact ~= nil then table.insert(entry,'"contact":'..tostring(contact == true)) end
                pcall(surfaceInfo,w,entry) -- Optional data must never interrupt motion export.
                table.insert(wheels,"{"..table.concat(entry,",").."}")
            end
        end
    end
    table.insert(parts,'"wheels":['..table.concat(wheels,",")..']')
    return "{"..table.concat(parts,",").."}"
end
function FarmMotionTelemetry:close()
    if ctx.file ~= nil then pcall(function() ctx.file:close() end) end
    ctx.file=nil
end
function FarmMotionTelemetry:deleteMap() self:close() end
function FarmMotionTelemetry:update(dt)
    if not finite(dt) or dt <= 0 then return end
    ctx.elapsed=ctx.elapsed+dt
    ctx.pending=ctx.pending+dt
    if ctx.pending < 16 then return end
    local step=ctx.pending
    ctx.pending=0
    ctx.seq=ctx.seq+1
    -- Keep a healthy pipe open. Periodic closure races the receiver and caused
    -- a full retry interval of missing telemetry during otherwise normal driving.
    if ctx.file == nil then
        ctx.retry=ctx.retry+step
        if ctx.retry < 1000 then return end
        ctx.retry=0
        local ok,f=pcall(io.open,ctx.pipeName,"w")
        if not ok or f == nil then return end
        ctx.file=f
    end
    local ok,line=pcall(self.buildLine,self)
    if not ok then
        -- A malformed/unavailable game state must disarm the consumer.
        line='{"v":1,"active":false}'
    end
    local wrote,err=pcall(function()
        local r,e=ctx.file:write(line.."\n")
        if r == nil then return e or "pipe write failed" end
        local flushed,flushError=ctx.file:flush()
        if flushed == nil then return flushError or "pipe flush failed" end
    end)
    if not wrote or err ~= nil then self:close();ctx.retry=1000 end
end
addModEventListener(FarmMotionTelemetry)
