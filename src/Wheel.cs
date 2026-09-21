// GPL-2.0-only. Windows DirectInput constant-force output.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FarmMotion {
    public sealed class Wheel : IDisposable {
        [DllImport("dinput8.dll")] static extern int DirectInput8Create(IntPtr module,uint version,ref Guid iid,out IntPtr result,IntPtr outer);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Simple(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int EnumCallback(IntPtr info,IntPtr context);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int EnumDevices(IntPtr self,uint type,EnumCallback callback,IntPtr context,uint flags);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateDevice(IntPtr self,ref Guid guid,out IntPtr device,IntPtr outer);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int EnumObjects(IntPtr self,EnumCallback callback,IntPtr context,uint flags);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int SetFormat(IntPtr self,ref DataFormat format);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int SetCoop(IntPtr self,IntPtr hwnd,uint flags);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateEffect(IntPtr self,ref Guid guid,ref Effect effect,out IntPtr handle,IntPtr outer);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int SetEffect(IntPtr self,ref Effect effect,uint flags);
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct DeviceInfo {
            public uint size; public Guid instance,product; public uint type;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string instanceName;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string productName;
            public Guid ff; public ushort usagePage,usage;
        }
        [StructLayout(LayoutKind.Sequential)] struct ObjectFormat { public IntPtr guid; public uint offset,type,flags; }
        [StructLayout(LayoutKind.Sequential)] struct DataFormat { public uint size,objectSize,flags,dataSize,count; public IntPtr objects; }
        [StructLayout(LayoutKind.Sequential)] struct Effect {
            public uint size,flags,duration,samplePeriod,gain,trigger,repeat,axesCount;
            public IntPtr axes,direction,envelope; public uint parameterSize; public IntPtr parameters; public uint startDelay;
        }
        public sealed class Info { public Guid Id; public string Name { get; set; } public override string ToString() { return Id+"  "+Name; } }
        IntPtr input,device,effect,axis,direction,magnitude; Form window; Effect spec;
        static T Method<T>(IntPtr p,int slot) where T:class { return Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(p),slot*IntPtr.Size),typeof(T)) as T; }
        static void Check(int result,string operation) { if(result<0) throw new InvalidOperationException(operation+" failed: 0x"+result.ToString("X8")); }
        public Wheel() { var iid=new Guid("BF798031-483A-4DA2-AA99-5D64ED369700"); Check(DirectInput8Create(GetModuleHandle(null),0x800,ref iid,out input,IntPtr.Zero),"DirectInput"); }
        public List<Info> List() {
            var list=new List<Info>();
            EnumCallback callback=delegate(IntPtr p,IntPtr c) { var d=(DeviceInfo)Marshal.PtrToStructure(p,typeof(DeviceInfo)); list.Add(new Info { Id=d.instance,Name=d.productName }); return 1; };
            Check(Method<EnumDevices>(input,4)(input,4,callback,IntPtr.Zero,0x101),"Enumerate wheels"); GC.KeepAlive(callback); return list;
        }
        public void Open(Guid id) {
            Check(Method<CreateDevice>(input,3)(input,ref id,out device,IntPtr.Zero),"Open wheel");
            uint objectType=0;
            EnumCallback callback=delegate(IntPtr p,IntPtr c) { if((Marshal.ReadInt32(p,28)&1)!=0) { objectType=(uint)Marshal.ReadInt32(p,24); return 0; } return 1; };
            Check(Method<EnumObjects>(device,4)(device,callback,IntPtr.Zero,3),"Find force axis"); GC.KeepAlive(callback);
            if(objectType==0) throw new InvalidOperationException("Wheel has no force-feedback axis");
            var obj=new ObjectFormat { type=objectType };
            IntPtr objPtr=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(ObjectFormat)));
            try {
                Marshal.StructureToPtr(obj,objPtr,false);
                var format=new DataFormat { size=(uint)Marshal.SizeOf(typeof(DataFormat)),objectSize=(uint)Marshal.SizeOf(typeof(ObjectFormat)),flags=1,dataSize=4,count=1,objects=objPtr };
                Check(Method<SetFormat>(device,11)(device,ref format),"Set axis format");
            } finally { Marshal.FreeHGlobal(objPtr); }
            window=new Form(); var hwnd=window.Handle;
            Check(Method<SetCoop>(device,13)(device,hwnd,9),"Acquire background exclusive access");
            Check(Method<Simple>(device,7)(device),"Acquire wheel");
            axis=Marshal.AllocHGlobal(4); direction=Marshal.AllocHGlobal(4); magnitude=Marshal.AllocHGlobal(4);
            Marshal.WriteInt32(axis,0); Marshal.WriteInt32(direction,10000); Marshal.WriteInt32(magnitude,0);
            spec=new Effect { size=(uint)Marshal.SizeOf(typeof(Effect)),flags=0x12,duration=50000,gain=10000,trigger=0xffffffff,axesCount=1,axes=axis,direction=direction,parameterSize=4,parameters=magnitude };
            var constant=new Guid("13541C20-8E33-11D0-9AD0-00A0C9A06E35");
            Check(Method<CreateEffect>(device,18)(device,ref constant,ref spec,out effect,IntPtr.Zero),"Create finite force effect");
        }
        public void Write(double value) {
            if(effect==IntPtr.Zero) return;
            if(double.IsNaN(value)||double.IsInfinity(value)) value=0;
            if(Math.Abs(value)<0.0001) { Check(Method<Simple>(effect,8)(effect),"Stop effect"); return; }
            Marshal.WriteInt32(magnitude,(int)(Math.Max(-0.10,Math.Min(0.10,value))*10000));
            Check(Method<SetEffect>(effect,6)(effect,ref spec,0x20000100),"Update force");
        }
        public void Dispose() {
            if(effect!=IntPtr.Zero) { Method<Simple>(effect,8)(effect); Marshal.Release(effect); effect=IntPtr.Zero; }
            if(device!=IntPtr.Zero) { Method<Simple>(device,8)(device); Marshal.Release(device); device=IntPtr.Zero; }
            if(input!=IntPtr.Zero) { Marshal.Release(input); input=IntPtr.Zero; }
            foreach(var p in new[]{axis,direction,magnitude}) if(p!=IntPtr.Zero) Marshal.FreeHGlobal(p);
            axis=direction=magnitude=IntPtr.Zero; if(window!=null) { window.Dispose(); window=null; }
        }
    }
}
