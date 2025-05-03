using System;

namespace Wesh {
    public class WeshArray {
        public static string CreateArray(Scope scope, string[] elements){
            var obj = new WeshObject();
            obj.SetProperty("length", "0");

            obj.SetProperty("add", Util.CreateAnonFunction(scope, new string[]{"elem"}, "arr.add " + obj.Name + " " + WESH.Lang["variableMark"] + "elem"));
            obj.SetProperty("get", Util.CreateAnonFunction(scope, new string[]{"elem"}, "return " + WESH.Lang["subCommandStart"] + "obj.get " + obj.Name + " " + WESH.Lang["variableMark"] + "elem" + WESH.Lang["subCommandEnd"]));
            obj.SetProperty("set", Util.CreateAnonFunction(scope, new string[]{"elem", "value"}, "obj.set " + obj.Name + " " + WESH.Lang["variableMark"] + "elem " + WESH.Lang["variableMark"] + "value"));


            foreach(string el in elements){
                AddElement(obj, el);
            }

            return obj.Name;
        }

        public static void AddElement(string name, string elem){
            var obj = WeshObject.GetObject(name);
            int oldLen = int.Parse(obj.GetProperty("length"));
            obj.SetProperty(oldLen.ToString(), elem);
            obj.SetProperty("length", (oldLen + 1).ToString());
        }

        static void AddElement(WeshObject obj, string elem){
            int oldLen = int.Parse(obj.GetProperty("length"));
            obj.SetProperty(oldLen.ToString(), elem);
            obj.SetProperty("length", (oldLen + 1).ToString());
        }
    }
}