using System;
using System.Linq;
using System.Reflection;
using System.Linq.Expressions;

namespace Wesh {
    class Util {
        public delegate void UniversalDelegate(params object[] args);

        public static string GenerateName(string name){
            return "__WESH_" + name + "_" + Guid.NewGuid().ToString();
        }

        public static string EscapeRegex(string ch){
            if(new string[]{"^", "$", "?", "\\", "(", ")", "{", "}", "[", "]", "|"}.Contains(ch)){
                return "\\" + ch;
            }

            return ch;
        }

        public static string CreateAnonFunction(Scope scope, string[] args, string code){
            string name = GenerateName("FUNCTION");

            var func = new Function();
            func.Arguments = args;
            func.Code = code;
            scope.SetFunction(name, func);

            return name;
        }

        public static string CreateAnonFunction(Scope scope, WESH.Command caller){
            string name = GenerateName("FUNCTION");

            var func = new Function();
            func.NativeCaller = caller;
            scope.SetFunction(name, func);

            return name;
        }

        public static Function GetAnonFunction(WESH.Command caller){
            var func = new Function();
            func.NativeCaller = caller;

            return func;
        }

        public static Delegate ConvertDelegate(UniversalDelegate original, Type targetDelegateType){
            var invokeMethod = targetDelegateType.GetMethod("Invoke");
            var pars = invokeMethod.GetParameters();
            var paramExpressions = pars.Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray();

            var argsArray = Expression.NewArrayInit(typeof(object), paramExpressions.Select(p =>(p.Type.IsValueType?(Expression)Expression.Convert(p, typeof(object)):p)));
            var callOriginal = Expression.Invoke(Expression.Constant(original), argsArray);

            Expression body;
            if(invokeMethod.ReturnType == typeof(void)){
                body = Expression.Block(callOriginal, Expression.Empty());
            }else{
                body = Expression.Convert(callOriginal, invokeMethod.ReturnType);
            }

            var lambda = Expression.Lambda(targetDelegateType, body, paramExpressions);
            return lambda.Compile();
        }

    }
}