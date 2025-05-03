using System;
using System.Linq;
using System.Text;
using System.Data;
using System.Threading;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq.Expressions;

namespace Wesh {
    public class Declaration<T> {
        public T Value;
        public Scope Scope;
    }

    public class Function {
        public string Code;
        public string[] Arguments;
        public WESH.Command NativeCaller;

        public string Invoke(string[] args, Scope scope){
            var funcScope = new Scope(scope);
            //funcScope.SetVariable("this", scope);

            if(NativeCaller != null){
                return NativeCaller(args, scope);
            }

            for(int i = 0; i < this.Arguments.Length; i++){
                if(args.Length > i){
                    funcScope.SetVariable(this.Arguments[i], args[i]);
                }else{
                    funcScope.SetVariable(this.Arguments[i], null);
                }
            }

            funcScope.SetVariable("args", WeshArray.CreateArray(scope, args));

            return WESH.ExecBlock(this.Code, funcScope);
        }
    }

    public class Scope {
        public static Scope Global;

        public Dictionary<string, string> Variables;
        public Dictionary<string, Function> Functions;
        public Scope ParentScope;

        public Scope(){
            Variables = new Dictionary<string, string>();
            Functions = new Dictionary<string, Function>();
        }

        public Scope(Scope parent){
            Variables = new Dictionary<string, string>();
            Functions = new Dictionary<string, Function>();
            ParentScope = parent;
        }

        public Declaration<string> GetVariable(string name){
            string vName = null;
            string[] oPath = null;
            string objectPropertyDelim = WESH.Lang["objectPropertyDelim"];

            if(name.Contains(objectPropertyDelim)){
                oPath = name.Split(new string[]{objectPropertyDelim}, StringSplitOptions.None);
                vName = oPath[0];
                oPath = oPath.Skip(1).ToArray();
            }else{
                vName = name;
            }

            if(Variables.ContainsKey(vName)){
                var dec = new Declaration<string>();
                dec.Value = Variables[vName];
                dec.Scope = this;

                if(oPath != null){
                    IObject obj;

                    foreach(string prName in oPath){
                        obj = ObjectStore.GetObject(dec.Value);

                        if(obj == null){
                            break;
                        }

                        dec.Value = obj.GetProperty(prName);
                    }
                }

                return dec;
            }


            if(ParentScope != null){
                return ParentScope.GetVariable(name);
            }

            return null;
        }

        public Declaration<Function> GetFunction(string name){
            if(Functions.ContainsKey(name)){
                var dec = new Declaration<Function>();
                dec.Value = Functions[name];
                dec.Scope = this;

                return dec;
            }

            if(ParentScope != null){
                return ParentScope.GetFunction(name);
            }

            return null;
        }

        public void SetVariable(string name, string value){
            string objectPropertyDelim = WESH.Lang["objectPropertyDelim"];

            if(name.Contains(objectPropertyDelim)){
                string[] oPath = name.Split(new string[]{objectPropertyDelim}, StringSplitOptions.None);
                string vName = oPath[0];
                oPath = oPath.Skip(1).ToArray();

                IObject obj;
                string val = GetVariable(vName).Value;
                int i = 0;

                foreach(string prName in oPath){
                    obj = ObjectStore.GetObject(val);

                    if(i == oPath.Length - 1){
                        obj.SetProperty(prName, value);
                        break;
                    }

                    val = obj.GetProperty(prName);
                    i++;
                }
            }else{
                if(Variables.ContainsKey(name)){
                    Variables[name] = value;
                }else{
                    Variables.Add(name, value);
                }
            }
        }

        public void SetFunction(string name, Function value){
            if(Functions.ContainsKey(name)){
                Functions[name] = value;
            }else{
                Functions.Add(name, value);
            }
        }

        public string[] ListVariables(){
            return Variables.Keys.ToArray();
        }

        public string[] ListFunctions(){
            return Functions.Keys.ToArray();
        }

        public bool HasVariable(string name){
            return Variables.ContainsKey(name);
        }

        public bool HasFunction(string name){
            return Functions.ContainsKey(name);
        }
    }

    public class StopExecution : Exception {
        public string Value;

        public StopExecution(string val){
            Value = val;
        }
    }

    public partial class WESH {
        public static readonly string WeshNull = "__WESH_NULL";

        public static Dictionary<string, string> Lang = new Dictionary<string, string>(){
            {"blockStart", "{"},
            {"blockEnd", "}"},
            {"escapeChar", "\\"},
            {"commandEnd", ";"},
            {"q1", "'"},
            {"q2", "\""},
            {"argDelim", ","},
            {"subCommandStart", "("},
            {"subCommandEnd", ")"},
            {"comment", "#"},
            {"equals", "="},
            {"variableMark", "@"},
            {"subCommandMark", "&"},
            {"objectPropertyDelim", "."}
        };

        static Dictionary<string, string> strSpec = new Dictionary<string, string>();
        static Dictionary<string, string> cStrSpec = new Dictionary<string, string>();
        static Dictionary<string, string> blockSpec = new Dictionary<string, string>();
        static Dictionary<string, string> subCmdSpec = new Dictionary<string, string>();

        static DataTable dataTable = new DataTable();

        public static string ExecScript(string scriptCode){
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            var globalScope = new Scope();
            Scope.Global = globalScope;
            return ExecBlock(scriptCode, globalScope);
        }

        public static string ExecBlock(string blockCode, Scope scope, bool cAllowConst){
            string commandEnd = Lang["commandEnd"];

            var sb = new StringBuilder();

            char blockStart = Lang["blockStart"][0];
            char blockEnd = Lang["blockEnd"][0];
            char escapeChar = Lang["escapeChar"][0];
            char q1 = Lang["q1"][0];
            char q2 = Lang["q2"][0];
            char subCommandStart = Lang["subCommandStart"][0];
            char subCommandEnd = Lang["subCommandEnd"][0];

            var _strSpec = new Dictionary<string, StringBuilder>();
            var _cStrSpec = new Dictionary<string, StringBuilder>();
            var _blockSpec = new Dictionary<string, StringBuilder>();
            var _subCmdSpec = new Dictionary<string, StringBuilder>();

            int depth = 0;
            int scDepth = 0;
            char qch = '\0';
            char pch = '\0';
            string csName = null;
            string sName = null;
            string bName = null;
            string scName = null;
            var builderStack = new List<StringBuilder>();
            builderStack.Add(sb);

            foreach(char ch in blockCode){
                if(pch == escapeChar){
                    builderStack[builderStack.Count - 1].Append(ch);
                }else if(ch == escapeChar && pch != escapeChar){
                    //ignore
                }else if(qch == '\0' && ch == q1){
                    qch = ch;
                    csName = "<?cstr-" + cStrSpec.Count.ToString() + "?>";
                    builderStack[builderStack.Count - 1].Append(csName);
                    builderStack.Add(new StringBuilder());
                    _cStrSpec.Add(csName, builderStack[builderStack.Count - 1]);
                }else if(qch == '\0' && ch == q2){
                    qch = ch;
                    sName = "<?str-" + strSpec.Count.ToString() + "?>";
                    builderStack[builderStack.Count - 1].Append(sName);
                    builderStack.Add(new StringBuilder());
                    _strSpec.Add(sName, builderStack[builderStack.Count - 1]);
                }else if(qch != '\0' && ch == qch){
                    if(qch == q1){
                        cStrSpec.Add(csName, _cStrSpec[csName].ToString());
                        csName = null;
                    }else{
                        strSpec.Add(sName, _strSpec[sName].ToString());
                        sName = null;
                    }

                    builderStack.RemoveAt(builderStack.Count - 1);
                    qch = '\0';
                }else if(qch != '\0'){
                    builderStack[builderStack.Count - 1].Append(ch);
                }else if(ch == subCommandStart){
                    if(scDepth == 0){
                        scName = "<?subcmd-" + subCmdSpec.Count.ToString() + "?>";
                        builderStack[builderStack.Count - 1].Append(scName);
                        builderStack.Add(new StringBuilder());
                        _subCmdSpec.Add(scName, builderStack[builderStack.Count - 1]);
                    }else{
                        _subCmdSpec[scName].Append(subCommandStart);
                    }

                    scDepth++;
                }else if(ch == subCommandEnd){
                    scDepth--;

                    if(scDepth == 0){
                        subCmdSpec.Add(scName, _subCmdSpec[scName].ToString());
                        scName = null;
                        builderStack.RemoveAt(builderStack.Count - 1);
                    }else{
                        _subCmdSpec[scName].Append(subCommandEnd);
                    }
                }else if(ch == blockStart){
                    if(depth == 0){
                        bName = "<?block-" + blockSpec.Count.ToString() + "?>";
                        builderStack[builderStack.Count - 1].Append(bName);
                        builderStack.Add(new StringBuilder());
                        _blockSpec.Add(bName, builderStack[builderStack.Count - 1]);
                    }else{
                        _blockSpec[bName].Append(blockStart);
                    }

                    depth++;
                }else if(ch == blockEnd){
                    depth--;

                    if(depth == 0){
                        blockSpec.Add(bName, _blockSpec[bName].ToString());
                        bName = null;
                        builderStack.RemoveAt(builderStack.Count - 1);
                    }else{
                        _blockSpec[bName].Append(blockEnd);
                    }
                }else{
                    builderStack[builderStack.Count - 1].Append(ch);
                }

                pch = ch;
            }

            //Console.WriteLine("PARSED\n" + sb.ToString() + "\n");

            string result = string.Empty;
            var code = sb.ToString().Replace("\r\n", commandEnd).Replace("\r", commandEnd).Replace("\n", commandEnd).Split(commandEnd[0]);
            foreach(string line in code){
                string c = null;

                try{
                    c = ExecCommand(line, scope, cAllowConst);
                    if(!string.IsNullOrEmpty(c)) result = c + "\n";
                }catch(StopExecution e){
                    c = e.Value;
                    if(!string.IsNullOrEmpty(c)) result = c + "\n";
                    break;
                }
            }

            return result.TrimEnd();
        }

        public static string ExecBlock(string blockCode, Scope scope){
            return ExecBlock(blockCode, scope, false);
        }

        public static string ExecCommand(string line, Scope scope, bool allowConstValues){
            try{
                if(string.IsNullOrEmpty(line)) return "";
                if(line.StartsWith(Lang["comment"])) return "";
                line = line.Split(new string[]{Lang["comment"]}, StringSplitOptions.None)[0].TrimStart();

                string[] aArgs = line.Replace(Lang["argDelim"], "").Split(' ');
                string[] rawArgs = (string[])aArgs.Skip(1).ToArray().Clone();

                for(int i = 0; i < aArgs.Length; i++){
                    string arg = aArgs[i];

                    if(blockSpec.ContainsKey(arg)){
                        aArgs[i] = blockSpec[arg];
                    }else if(cStrSpec.ContainsKey(arg)){
                        aArgs[i] = cStrSpec[arg];
                    }else if(strSpec.ContainsKey(arg)){
                        aArgs[i] = ParseArg(strSpec[arg], scope);
                    }else if(subCmdSpec.ContainsKey(arg)){
                        aArgs[i] = ExecBlock(subCmdSpec[arg], scope);
                    }else{
                        aArgs[i] = ParseArg(arg, scope);
                    }
                }

                string cmd = aArgs[0];
                if(cmd == null) return "";

                string[] args = aArgs.Skip(1).ToArray();
                string cmdName = null;

                if(Commands.ContainsKey(cmd)){
                    cmdName = cmd;
                }else if(Aliases.ContainsKey(cmd)){
                    cmdName = Aliases[cmd];
                }

                if(!string.IsNullOrEmpty(cmdName)){
                    return CheckNull(Commands[cmdName](args, scope));
                }

                var func = scope.GetFunction(cmd);
                if(func != null){
                    return CheckNull(func.Value.Invoke(args, scope));
                }

                if(strSpec.ContainsKey(cmd)){
                    return strSpec[cmd];
                }

                if(cStrSpec.ContainsKey(cmd)){
                    return cStrSpec[cmd];
                }
                var all = args.ToList();
                all.Insert(0, cmd);
                string expr = string.Join(" ", all);

                if(Regex.IsMatch(expr, @"^[0-9\-\.]+$")){
                    return cmd;
                }

                if(Regex.IsMatch(expr, @"^([0-9\.\+\-\*/\<\>\=\s%\&\|!]|(true)|(false)|(not)|(and)|(or)|(xor))+$")){
                    return ExecExpression(expr);
                }

                if(args.Length > 1){
                    string sign = args[0];
                    string joined = string.Join(" ", rawArgs.Skip(1));
                    string val;

                    if(sign == Lang["equals"]){
                        val = ExecBlock(joined, scope, true);
                    }else{
                        return "ERROR: Invalid command";
                    }

                    //Console.WriteLine("Setting " + cmd + " to " + val);

                    if(!scope.HasVariable(cmd)){
                        scope.SetVariable(cmd, val);
                    }else{
                        var vr = scope.GetVariable(cmd);
                        vr.Scope.SetVariable(cmd, val);
                    }


                    return CheckNull(val);
                }

                if(allowConstValues){
                    return cmd;
                }else{
                    return "ERROR: Invalid command";
                }
            }catch(StopExecution e){
                throw e;
            }catch(Exception e){
                //Console.WriteLine("WeshError " + e);
                return "ERROR: " + e;
            }
        }

        public static string ExecCommand(string line, Scope scope){
            return ExecCommand(line, scope, false);
        }

        static string ParseArg(string arg, Scope scope){
            string variableMark = Lang["variableMark"];
            string subCommandMark = Lang["subCommandMark"];

            if(arg.StartsWith(variableMark)){
                var vr = scope.GetVariable(arg.Substring(variableMark.Length));
                if(vr != null) return vr.Value;
            }

            if(arg.StartsWith(subCommandMark)){
                return ExecBlock(arg.Substring(subCommandMark.Length), scope);
            }

            arg = Regex.Replace(arg, Util.EscapeRegex(variableMark) + @"([a-zA-Z0-9\._\-/]+)", new MatchEvaluator((m)=>{
                var vr = scope.GetVariable(m.Groups[1].ToString());
                if(vr != null) return vr.Value;
                return "";
            }));

            arg = Regex.Replace(arg, Util.EscapeRegex(variableMark) + @"\{([^\}]+)\}", new MatchEvaluator((m)=>{
                var vr = scope.GetVariable(m.Groups[1].ToString());
                if(vr != null) return vr.Value;
                return "";
            }));

            arg = Regex.Replace(arg, Util.EscapeRegex(subCommandMark) + @"\{([^\}]+)\}", new MatchEvaluator((m)=>{
                return ExecBlock(m.Groups[1].ToString(), scope);
            }));

            return arg;
        }

        public static string ExecExpression(string expr){
            try{
                return dataTable.Compute(expr.Replace("&&", " and ").Replace("||", " or ").Replace("!", "not "), "").ToString().ToLower();
            }catch(Exception){
                return expr;
            }
        }

        public static bool CheckBoolExpression(string expr){
            return !(expr == "false" || expr == "0");
        }

        public static string CheckNull(string s){
            return (s == null)?WeshNull:s;
        }

        public static string CheckNull(object o){
            return (o == null)?WeshNull:o.ToString();
        }
    }
}