using System;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Wesh {
    public enum InteropObjectType {
        NetObject,
        NetClass,
        ComObject
    }

    public class WeshInterop {
        public static List<Assembly> LoadedAssemblies = new List<Assembly>();
        public static bool IsDefaultAssembliesLoaded = false;
        public static Dictionary<string, object> VObjectMapping = new Dictionary<string, object>();

        public static object[] ConvertArgs(string[] sArgs){
            var args = new List<object>();

            foreach(string arg in sArgs){
                int iv = 0;
                double dv = 0;
                if(int.TryParse(arg, out iv)) args.Add(iv);
                else if(double.TryParse(arg, out dv)) args.Add(dv);
                else if(arg == "true" || arg == "false") args.Add((arg == WESH.WeshNull)?null:arg);
                else if(VObjectMapping.ContainsKey(arg)) args.Add(VObjectMapping[arg]);
                else args.Add(arg);
            }

            return args.ToArray();
        }

        public static void LoadAssemblies(){
            if(!IsDefaultAssembliesLoaded){
                LoadedAssemblies.AddRange(AppDomain.CurrentDomain.GetAssemblies());
                IsDefaultAssembliesLoaded = true;
            }
        }

        static string HandleOutput(Scope scope, object o){
            if(o == null) return WESH.WeshNull;

            Type t = o.GetType();

            switch(t.FullName){
                case "System.String": {
                    return o.ToString();
                }
                case "System.Int32":
                case "System.Int64":
                case "System.IntPtr":
                case "System.Single":
                case "System.Double": {
                    return Convert.ChangeType(o, t).ToString();
                }
                case "System.Boolean": {
                    return ((bool)o)?"true":"false";
                }
                default: {
                    return SetupObject(scope, t, o).Name;
                }
            }
        }

        static WeshVirtualObject SetupObject(Scope scope, Type t, object o){
            var vo = new WeshVirtualObject(scope);

            foreach(var pi in t.GetProperties()){
                vo.DefineProperty(new VirtualObjectProperty(){
                    Name = pi.Name,
                    Type = VirtualObjectPropertyType.Getter,
                    Handler = VirtualObjectPropertyHandler.Native,
                    NativeGetter = ()=>{
                        return HandleOutput(scope, t.InvokeMember(pi.Name, BindingFlags.GetProperty, null, o, new object[0]));
                    }
                });

                vo.DefineProperty(new VirtualObjectProperty(){
                    Name = pi.Name,
                    Type = VirtualObjectPropertyType.Setter,
                    Handler = VirtualObjectPropertyHandler.Native,
                    NativeSetter = (string val)=>{
                        t.InvokeMember(pi.Name, BindingFlags.SetProperty, null, o, ConvertArgs(new string[]{ val }));
                    }
                });
            }

            foreach(var mi in t.GetMethods()){
                var fName = Util.CreateAnonFunction(scope, (iArgs, _)=>{
                    return HandleOutput(scope, t.InvokeMember(mi.Name, BindingFlags.InvokeMethod, null, o, ConvertArgs(iArgs)));
                });

                vo.DefineProperty(new VirtualObjectProperty(){
                    Name = mi.Name,
                    Type = VirtualObjectPropertyType.Getter,
                    Handler = VirtualObjectPropertyHandler.Native,
                    NativeGetter = ()=>{
                        return fName;
                    }
                });
            }

            VObjectMapping.Add(vo.Name, o);
            return vo;
        }

        public static string Create(InteropObjectType type, string name, string[] constArgs, Scope scope){
            var args = ConvertArgs(constArgs);
            Type t = null;
            object o = null;

            if(type == InteropObjectType.NetObject){
                LoadAssemblies();
                foreach(Assembly asm in LoadedAssemblies){
                    t = asm.GetType(name);
                    if(t != null) break;
                }

                o = Activator.CreateInstance(t, args);
            }else if(type == InteropObjectType.NetClass){
                LoadAssemblies();
                foreach(Assembly asm in LoadedAssemblies){
                    t = asm.GetType(name);
                    if(t != null) break;
                }
            }else if(type == InteropObjectType.ComObject){
                t = Type.GetTypeFromProgID(name);
                o = Activator.CreateInstance(t);

                var vo = new WeshVirtualObject(scope);

                vo.DefineProperty(new VirtualObjectProperty(){
                    Type = VirtualObjectPropertyType.DefaultGetter,
                    Handler = VirtualObjectPropertyHandler.Native,
                    NativeDefaultGetter = (pName)=>{
                        try{
                            return HandleOutput(scope, t.InvokeMember(pName, BindingFlags.GetProperty, null, o, new object[0]));
                        }catch(COMException){
                            return Util.CreateAnonFunction(scope, (fArgs, fScope)=>{
                                return HandleOutput(scope, t.InvokeMember(pName, BindingFlags.InvokeMethod, null, o, ConvertArgs(fArgs)));              
                            });
                        }
                    }
                });

                VObjectMapping.Add(vo.Name, o);
                return vo.Name;
            }

            return SetupObject(scope, t, o).Name;
        }
    }
}