"""Run with Python + lupa; simulated FS25 APIs, never opens real pipes/devices."""
from pathlib import Path
from lupa import LuaRuntime
import json
script=(Path(__file__).resolve().parents[1]/"mod/FarmMotionTelemetry.lua").read_text(encoding="utf-8")
count=0
def check(value, label):
    global count
    assert value,label
    count+=1
    print("PASS:",label)
def setup():
    lua=LuaRuntime(unpack_returned_tuples=True)
    lua.execute("""
        opens=0; writes={}; warnings={}; closed=0
        g_currentMission={}
        localVehicle={rootNode=10,components={{node=10}},spec_wheels={wheels={{netInfo={y=.25}}}}}
        remoteVehicle={rootNode=99}
        g_localPlayer={getCurrentVehicle=function(self) assert(self==g_localPlayer); return localVehicle end}
        g_gui={getIsGuiVisible=function(self) return false end}
        Logging={warning=function(fmt,msg) table.insert(warnings,msg) end}
        entityExists=function(node) return node==10 end
        getLinearVelocity=function(node) return 0,.5,0 end
        addModEventListener=function(value) listener=value end
        fakeFile={
            write=function(self,line) table.insert(writes,line); return self end,
            flush=function(self) return true end,
            close=function(self) closed=closed+1; return true end
        }
        io.open=function() opens=opens+1; return fakeFile end
    """)
    lua.execute(script)
    return lua
l=setup();l.execute("listener:update(16)")
packet=json.loads(l.eval("writes[1]"))
check(packet["active"] and packet["vehicle"]=="10" and packet["wheels"][0]["y"]==.25,"local player and instance-method vehicle lookup")
check(packet["vy"]==.5,"guarded velocity export")
l.execute("g_dedicatedServer={}; listener:update(16)")
check(l.eval("opens")==1 and l.eval("closed")==1,"dedicated-server transition closes pipe without reopening")
l=setup();l.execute("g_dedicatedServer={}; listener:update(16)")
check(l.eval("opens")==0,"dedicated server never opens a pipe")
l=setup();l.execute("g_localPlayer=nil; listener:update(16)")
check(l.eval("opens")==0,"client before local-player creation stays idle")
l=setup();l.execute("g_currentMission.isPaused=true; listener:update(16)")
check(not json.loads(l.eval("writes[1]"))["active"],"pause exports inactive telemetry")
l=setup();l.execute("g_gui.getIsGuiVisible=function() return true end; listener:update(16)")
check(not json.loads(l.eval("writes[1]"))["active"],"menu exports inactive telemetry")
l=setup();l.execute("io.open=function() opens=opens+1; return nil,'not running' end; for i=1,10 do listener:update(16) end")
check(l.eval("opens")==1 and l.eval("#warnings")==0,"missing companion retries without log spam")
l=setup();l.execute("fakeFile.write=function() return nil,'disconnected' end; listener:update(16); listener:update(16)")
check(l.eval("closed")==1 and l.eval("opens")==1 and l.eval("#warnings")==1,"write failure closes pipe and rate-limits reconnect")
l.execute("fakeFile.write=function(self,line) table.insert(writes,line); return self end; listener:update(1000)")
check(l.eval("opens")==2 and l.eval("#writes")==1,"reconnect resumes telemetry")
l=setup();l.execute("fakeFile.flush=function() return nil,'flush failed' end; listener:update(16)")
check(l.eval("closed")==1 and l.eval("#warnings")==1,"flush failure closes pipe and reports")
l=setup();l.execute("fakeFile.write=function(self,line) table.insert(writes,line) end; fakeFile.flush=function() flushes=(flushes or 0)+1 end; for i=1,180 do listener:update(16) end")
check(l.eval("#writes")==180 and l.eval("flushes")==180 and l.eval("opens")==1 and l.eval("closed")==0 and l.eval("#warnings")==0,"no-return write and flush keep continuous telemetry on one connection")
l=setup();l.execute("fakeFile.write=function() return false end; listener:update(16)")
check(l.eval("closed")==1,"explicit false write closes pipe")
l=setup();l.execute("fakeFile.flush=function() return false end; listener:update(16)")
check(l.eval("closed")==1,"explicit false flush closes pipe")
l=setup();l.execute("localVehicle.components={{node=0}}; getLinearVelocity=function() error('invalid node') end; listener:update(16)")
check("vy" not in json.loads(l.eval("writes[1]")),"invalid physics node is not queried")
l=setup()
try:
    l.execute("localVehicle.getLastSpeed=function() error('unexpected API failure') end; listener:update(16)")
    visible=False
except Exception as e:
    visible="unexpected API failure" in str(e)
check(visible,"unexpected API failure is not swallowed")
l=setup();l.execute("listener:update(16); listener:deleteMap(); listener:deleteMap()")
check(l.eval("closed")==1,"map unload closes exactly once")
def marker_setup():
    l=setup()
    l.execute('''
        getUserProfileAppPath=function() return 'test/' end
        marker=string.rep('a',32)..string.char(10)
        local openPipe=io.open
        io.open=function(name,mode)
            if mode=='r' then
                if marker==nil then return nil end
                return {read=function(self,size) assert(size==64); return marker end,close=function() end}
            end
            return openPipe(name,mode)
        end
    ''')
    return l
l=marker_setup();l.execute("for i=1,200 do listener:update(16) end")
check(l.eval('opens')==1 and l.eval('closed')==0,'unchanged marker never recycles healthy connection')
l.execute("fakeFile.write=function() end; fakeFile.flush=function() end; marker=string.rep('b',32)..string.char(10); listener:update(1000)")
check(l.eval('opens')==2 and l.eval('closed')==1,'new app token recovers even with silent broken-pipe writes')
l.execute("marker='partial'; listener:update(1000); marker=nil; listener:update(1000)")
check(l.eval('opens')==2 and l.eval('closed')==1,'missing and partial markers do not interrupt telemetry')
l.execute("marker=string.rep('c',32)..string.char(10); listener:update(1000)")
check(l.eval('opens')==3,'later valid token recovers after missing marker')
l=marker_setup();l.execute("marker=nil; listener:update(16); marker=string.rep('d',32)..string.char(10); listener:update(1000)")
check(l.eval('opens')==2,'first marker reconnects an already-open old connection')
print(str(count)+" telemetry tests passed")
