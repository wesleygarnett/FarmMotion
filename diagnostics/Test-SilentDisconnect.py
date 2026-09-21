"""Simulate a game IO wrapper that hides broken-pipe errors. No real IO/devices."""
from pathlib import Path
import runpy
fixture=runpy.run_path(str(Path(__file__).resolve().parents[1]/'tests/Test-Mod.py'))
l=fixture['setup']()
l.execute('listener:update(16)')
l.execute('fakeFile.write=function() end; fakeFile.flush=function() end; for i=1,1000 do listener:update(16) end')
assert l.eval('opens')==1 and l.eval('closed')==0
print('REPRODUCED: after 16 simulated seconds of silent broken writes, mod has not reopened the pipe')
l.execute("fakeFile.write=function() return nil,'broken pipe' end; listener:update(16); listener:update(1000)")
assert l.eval('opens')==2
print('CONTROL: explicit broken-pipe error causes a reopen after the retry interval')
