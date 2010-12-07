using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Diagnostics;

namespace StackExchange.AssetPackager
{
    class Program 
    {
        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: AssetPackager <filename> <rootweb>");
                Environment.Exit(-1);
                return;
            }

            string filename = System.IO.Path.GetFullPath(args[0]);
            string rootweb = System.IO.Path.GetFullPath(args[1]);
            
            try
            {
                Console.WriteLine(filename);
                var a = Assembly.LoadFile(filename);
                foreach (var t in a.GetTypes())
                {

                    if (t.BaseType != null && t.BaseType.IsGenericType && t.BaseType.GetGenericTypeDefinition() == typeof(Packager<>))
                    {
                        Console.WriteLine("Packing up css and js");
                        t.InvokeMember("PackIt", BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod, null, null, new object[] 
                    { 
                        rootweb
                    });
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("FAILED : " + e);
                Environment.Exit(-1);
            }

#if DEBUG
            Console.ReadKey();
#endif 
        }

    }
}
