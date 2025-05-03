using System;
using System.Collections.Generic;
using System.Linq;

namespace Wesh {
    public interface IObject {
        string GetProperty(string name);
        void SetProperty(string name, string value);
    }

    public class ObjectStore {
        public static Dictionary<string, IObject> Objects = new Dictionary<string, IObject>();

        public static IObject GetObject(string name){
            if(Objects.ContainsKey(name)){
                return Objects[name];
            }

            return null;
        }

        public static bool HasObject(string name){
            return Objects.ContainsKey(name);
        }
    }

    public class WeshObject : IObject {

        public static IObject GetObject(string name){
            if(ObjectStore.Objects.ContainsKey(name)){
                return ObjectStore.Objects[name];
            }

            return null;
        }

        public static void AddObject(WeshObject obj){
            ObjectStore.Objects.Add(obj.Name, obj);
        }

        Dictionary<string, string> Properties;
        public string Name;

        public WeshObject(){
            Properties = new Dictionary<string, string>();
            Name = Util.GenerateName("OBJECT");
            WeshObject.AddObject(this);
        }

        public string GetProperty(string name){
            if(Properties.ContainsKey(name)){
                return Properties[name];
            }

            return null;
        }

        public void SetProperty(string name, string value){
            if(Properties.ContainsKey(name)){
                Properties[name] = value;
            }else{
                Properties.Add(name, value);
            }
        }
    }

    public enum VirtualObjectPropertyHandler {
        Native,
        Wesh
    }

    public enum VirtualObjectPropertyType {
        Getter,
        Setter,
        DefaultGetter,
        DefaultSetter
    }

    public delegate string VirtualObjectPropertyGetter();
    public delegate void VirtualObjectPropertySetter(string val);

    public delegate string VirtualObjectPropertyDefaultGetter(string prop);
    public delegate void VirtualObjectPropertyDefaultSetter(string prop, string val);

    public struct VirtualObjectProperty {
        public string Name;
        public VirtualObjectPropertyType Type;
        public VirtualObjectPropertyHandler Handler;
        public Function WeshGetter;
        public Function WeshSetter;
        public Function WeshDefaultGetter;
        public Function WeshDefaultSetter;
        public VirtualObjectPropertyGetter NativeGetter;
        public VirtualObjectPropertySetter NativeSetter;
        public VirtualObjectPropertyDefaultGetter NativeDefaultGetter;
        public VirtualObjectPropertyDefaultSetter NativeDefaultSetter;
    }

    public class WeshVirtualObject : IObject {
        public static IObject GetObject(string name){
            if(ObjectStore.Objects.ContainsKey(name)){
                return ObjectStore.Objects[name];
            }

            return null;
        }

        public static void AddObject(WeshVirtualObject obj){
            ObjectStore.Objects.Add(obj.Name, obj);
        }

        List<VirtualObjectProperty> Properties;
        public string Name;
        public Scope MethodScope;

        public WeshVirtualObject(Scope scp){
            MethodScope = scp;
            Properties = new List<VirtualObjectProperty>();
            Name = Util.GenerateName("OBJECT");
            WeshVirtualObject.AddObject(this);

            DefineMethod("defineGetter", (args, scope)=>{
                DefineProperty(new VirtualObjectProperty(){
                    Name = args[0],
                    Type = VirtualObjectPropertyType.Getter,
                    Handler = VirtualObjectPropertyHandler.Wesh,
                    WeshGetter = scope.GetFunction(args[1]).Value
                });

                return string.Empty;
            });

            DefineMethod("defineSetter", (args, scope)=>{
                DefineProperty(new VirtualObjectProperty(){
                    Name = args[0],
                    Type = VirtualObjectPropertyType.Setter,
                    Handler = VirtualObjectPropertyHandler.Wesh,
                    WeshSetter = scope.GetFunction(args[1]).Value
                });

                return string.Empty;
            });

            DefineMethod("defineDefaultGetter", (args, scope)=>{
                DefineProperty(new VirtualObjectProperty(){
                    Type = VirtualObjectPropertyType.DefaultGetter,
                    Handler = VirtualObjectPropertyHandler.Wesh,
                    WeshDefaultGetter = scope.GetFunction(args[0]).Value
                });

                return string.Empty;
            });

            DefineMethod("defineDefaultSetter", (args, scope)=>{
                DefineProperty(new VirtualObjectProperty(){
                    Type = VirtualObjectPropertyType.DefaultSetter,
                    Handler = VirtualObjectPropertyHandler.Wesh,
                    WeshDefaultSetter = scope.GetFunction(args[0]).Value
                });

                return string.Empty;
            });
        }

        public string GetProperty(string name){
            var prl = Properties.Where((p)=>(p.Name == name && p.Type == VirtualObjectPropertyType.Getter));
            VirtualObjectProperty prop;

            if(prl.Count() == 0){
                var dgl = Properties.Where((p)=>(p.Type == VirtualObjectPropertyType.DefaultGetter));
                if(dgl.Count() == 0) return null;
                prop = dgl.ElementAt(0);

                if(prop.Handler == VirtualObjectPropertyHandler.Native){
                    return prop.NativeDefaultGetter(name);
                }else if(prop.Handler == VirtualObjectPropertyHandler.Wesh){
                    return prop.WeshDefaultGetter.Invoke(new string[]{ name }, MethodScope);
                }
            }else{
                prop = prl.ElementAt(0);

                if(prop.Handler == VirtualObjectPropertyHandler.Native){
                    return prop.NativeGetter();
                }else if(prop.Handler == VirtualObjectPropertyHandler.Wesh){
                    return prop.WeshGetter.Invoke(new string[0], MethodScope);
                }
            }

            return null;
        }

        public void SetProperty(string name, string value){
            var prl = Properties.Where((p)=>(p.Name == name && p.Type == VirtualObjectPropertyType.Setter));
            VirtualObjectProperty prop;

            if(prl.Count() == 0){
                var dsl = Properties.Where((p)=>(p.Type == VirtualObjectPropertyType.DefaultSetter));
                if(dsl.Count() == 0) return;
                prop = dsl.ElementAt(0);

                if(prop.Handler == VirtualObjectPropertyHandler.Native){
                    prop.NativeDefaultSetter(name, value);
                }else if(prop.Handler == VirtualObjectPropertyHandler.Wesh){
                    prop.WeshDefaultSetter.Invoke(new string[]{ name, value }, MethodScope);
                }
            }else{
                prop = prl.ElementAt(0);

                if(prop.Handler == VirtualObjectPropertyHandler.Native){
                    prop.NativeSetter(value);
                }else if(prop.Handler == VirtualObjectPropertyHandler.Wesh){
                    prop.WeshSetter.Invoke(new string[]{ value }, MethodScope);
                }
            }
        }

        public void DefineProperty(VirtualObjectProperty prop){
            Properties.Add(prop);
        }

        public void DefineMethod(string name, WESH.Command del){
            var func = Util.CreateAnonFunction(MethodScope, del);

            DefineProperty(new VirtualObjectProperty(){
                Name = name,
                Type = VirtualObjectPropertyType.Getter,
                Handler = VirtualObjectPropertyHandler.Native,
                NativeGetter = ()=>{
                    return func;
                }
            });
        }
    }
}