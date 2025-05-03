using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.IO;

namespace Wesh {
    public partial class WESH {
        public delegate string Command(string[] args, Scope scope);

        public static Dictionary<string, Command> Commands = new Dictionary<string, Command>(){
            {"testargs", (args, scope)=>{
                return "Args got:" + string.Join(", ", args);
            }},
            {"write", (args, _)=>{
                Console.Write(args[0]);
                return string.Empty;
            }},
            {"writeln", (args, _)=>{
                Console.WriteLine(args[0]);
                return string.Empty;
            }},
            {"readln", (args, _)=>{
                return Console.ReadLine();
            }},
            {"argcount", (args, _)=>{
                return args.Length.ToString() + " arguments";
            }},
            {"if", (args, scope)=>{
                if(CheckBoolExpression(ExecBlock(args[0], scope, true))){
                    return ExecBlock(args[1], new Scope(scope));
                }else{
                    for(int i = 2; i < args.Length - 2; i+=3){
                        if(args[i] == "elif" && args.Length > (i + 2)){
                            if(CheckBoolExpression(ExecBlock(args[i+1], new Scope(scope), true))){
                                return ExecBlock(args[i+2], new Scope(scope));
                            }
                        }
                    }

                    if(args[args.Length - 2] == "else"){
                        return ExecBlock(args[args.Length - 1], new Scope(scope));
                    }
                }

                return string.Empty;
            }},
            {"while", (args, scope)=>{
                var scp = new Scope(scope);
                while(CheckBoolExpression(ExecBlock(args[0], scp, true))){
                    ExecBlock(args[1], scp);
                }

                return string.Empty;
            }},
            {"for", (args, scope)=>{
                var scp = new Scope(scope);
                for(ExecBlock(args[0], scp, true); CheckBoolExpression(ExecBlock(args[1], scp, true)); ExecBlock(args[2], scp, true)){
                    ExecBlock(args[3], scp);
                }
                
                return string.Empty;
            }},
            {"func", (args, scope)=>{
                var func = new Function();
                func.Arguments = args.Skip(1).Take(args.Length - 2).ToArray();
                func.Code = args[args.Length - 1];

                scope.SetFunction(args[0], func);
                return string.Empty;
            }},
            {"afunc", (args, scope)=>{
                return Util.CreateAnonFunction(scope, args.Take(args.Length - 1).ToArray(), args[args.Length - 1]);
            }},
            {"return", (args, scope)=>{
                throw new StopExecution(ExecBlock(string.Join(" ", args), scope, true));
            }},
            {"obj.create", (args, _)=>{
                return new WeshObject().Name;
            }},
            {"obj.get", (args, _)=>{
                return ObjectStore.GetObject(args[0]).GetProperty(args[1]);
            }},
            {"obj.set", (args, _)=>{
                ObjectStore.GetObject(args[0]).SetProperty(args[1], args[2]);
                return string.Empty;
            }},
            {"createObject", (args, scope)=>{
                var bScope = new Scope(scope);
                ExecBlock(args[0], bScope);

                var obj = new WeshObject();
                foreach(string varName in bScope.ListVariables()){
                    obj.SetProperty(varName, bScope.GetVariable(varName).Value);
                }

                return obj.Name;
            }},
            {"arr.create", (args, scope)=>{
                return WeshArray.CreateArray(scope, args);
            }},
            {"arr.add", (args, _)=>{
                WeshArray.AddElement(args[0], args[1]);
                return string.Empty;
            }},
            {"vobj.create", (args, scope)=>{
                return new WeshVirtualObject(scope).Name;
            }},
            {"vobj.defineGetter", (args, scope)=>{
                ((WeshVirtualObject)ObjectStore.GetObject(args[0])).DefineProperty(new VirtualObjectProperty(){
                    Name = args[1],
                    Type = VirtualObjectPropertyType.Getter,
                    Handler = VirtualObjectPropertyHandler.Wesh,
                    WeshGetter = scope.GetFunction(args[2]).Value
                });

                return string.Empty;
            }},
            {"vobj.defineSetter", (args, scope)=>{
                ((WeshVirtualObject)ObjectStore.GetObject(args[0])).DefineProperty(new VirtualObjectProperty(){
                    Name = args[1],
                    Type = VirtualObjectPropertyType.Setter,
                    Handler = VirtualObjectPropertyHandler.Wesh,
                    WeshSetter = scope.GetFunction(args[2]).Value
                });

                return string.Empty;
            }},
            {"new", (args, scope)=>{
                switch(args[0]){
                    case "Object": {
                        return new WeshObject().Name;
                    }
                    case "Array": {
                        return WeshArray.CreateArray(scope, args.Skip(1).ToArray());
                    }
                    case "VirtualObject": {
                        return new WeshVirtualObject(scope).Name;
                    }
                    case "NetObject": {
                        return WeshInterop.Create(InteropObjectType.NetObject, args[1], args.Skip(2).ToArray(), scope);
                    }
                    case "NetClass": {
                        return WeshInterop.Create(InteropObjectType.NetClass, args[1], args.Skip(2).ToArray(), scope);
                    }
                    case "ComObject": {
                        return null;
                        //return WeshInterop.Create(InteropObjectType.ComObject, args[1], args.Skip(2).ToArray(), scope);
                    }
                    default: {
                        return null;
                    }
                }
            }},
            {"loadAssembly", (args, _)=>{
                WeshInterop.LoadedAssemblies.Add(Assembly.LoadWithPartialName(args[0]));
                return string.Empty;
            }},
            {"createDelegate", (args, scope)=>{
                var name = Util.GenerateName("DELEGATE");
                WeshInterop.VObjectMapping.Add(name, Util.ConvertDelegate((args2)=>{
                    ExecBlock(args[1], scope);
                }, Type.GetType(args[0])));

                return name;
            }},
            {"load", (args, scope)=>{
                if(File.Exists(args[0])){
                    ExecBlock(File.ReadAllText(args[0]), scope);
                }

                return string.Empty;
            }},
            {"str.equals", (args, _)=>{
                return (args[0] == args[1])?"true":"false";
            }},
            {"expr", (args, _)=>{
                return ExecExpression(args[0]);
            }}
        };

        public static Dictionary<string, string> Aliases = new Dictionary<string, string>(){
            {"object", "obj.create"},
            {"array", "arr.create"}
        };
    }
}