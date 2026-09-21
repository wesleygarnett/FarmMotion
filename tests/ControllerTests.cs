// GPL-2.0-only. Pure mapping tests; never initialize SDL or touch hardware.
using System;
namespace FarmMotion {
    static class ControllerTests {
        public static void Run(Action<bool,string> check) {
            check(ControllerOutput.Motor(double.NaN)==0 && ControllerOutput.Motor(double.PositiveInfinity)==0,"Rumble invalid values become silence");
            check(ControllerOutput.Motor(-1)==0 && ControllerOutput.Motor(2)==65535 && ControllerOutput.Motor(.5)==32768,"Rumble maps normalized amplitude to bounded motors");
            check(ControllerOutput.Fresh(1,.9) && !ControllerOutput.Fresh(1,.8) && !ControllerOutput.Fresh(1,2),"Rumble rejects stale and future frames");
            check(ControllerOutput.Smooth(0,1,.02)>.5 && ControllerOutput.Smooth(0,1,.02)<1 && ControllerOutput.Smooth(1,0,.02)>.5,"Rumble envelope has fast attack and slower bounded release");
            check(ControllerOutput.Identity(1,2,"serial","path",4)==ControllerOutput.Identity(1,2,"serial","other",5),"Controller serial selection survives reconnect instance change");
            check(ControllerOutput.Identity(1,2,"","path-a",4)!=ControllerOutput.Identity(1,2,"","path-b",4),"Identical controller models keep distinct path identities");
            check(ControllerOutput.Identity(1,2,"","",4)=="session:4","Controller without stable identity is explicitly session-only");
        }
    }
}
