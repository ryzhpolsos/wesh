using System;
using System.IO;

namespace Wesh {
    class Program {
        public static void Main(string[] args){
            if(args.Length > 0){
                string fileName = args[0];

                if(File.Exists(fileName)){
                    string code = File.ReadAllText(fileName);
                    WESH.ExecScript(code);
                    return;
                }else{
                    Console.WriteLine("ERROR: File \"" + fileName + "\" not found");
                    Environment.Exit(1);
                }
            }

            var cliScope = new Scope();
            while(true){
                Console.Write("[wesh] > ");
                Console.WriteLine(WESH.ExecBlock(Console.ReadLine(), cliScope, false));
            }
        }
    }
}
