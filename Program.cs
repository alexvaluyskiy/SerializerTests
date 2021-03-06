﻿using SerializerTests.Serializers;
using SerializerTests.TypesToSerialize;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SerializerTests
{
    /* 
     * Howto add your own serializer:
     * 
        1. Look at Serializers directory fore examples.
      
            You need to set the CreateNTestData delegate so the tester can serialize and deserialize it. 
            For the default settings you need only to override Serialize and Deserialize and call your formatter. The serializer type argument
            is used to print out the assemlby version of your serializer. You can use any type of the declaring assembly.

            public class BinaryFormatter<T> : TestBase<T, System.Runtime.Serialization.Formatters.Binary.BinaryFormatter> where T : class
            {
                public BinaryFormatter(Func<int,T> testData)
                {
                    base.CreateNTestData = testData;
                    FormatterFactory = () => new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                }

                protected override void Serialize(T obj, Stream stream)
                {
                    Formatter.Serialize(stream, obj);
                }

                protected override T Deserialize(Stream stream)
                {
                    return (T)Formatter.Deserialize(stream);
                }
           }

        2. Add your serializer to the list of serializers to be tested to the list of serializers in Deserialize, Serialize and FirstCall

        3. Recompile and run first the serialization test to create the test data on disk for the deserialization run.

        4. Publish the results on your own blog.
    }
    */


    class Program
    {
        static string Help = "SerializerTests is a serializer performance testing framework to evaluate and compare different serializers for .NET by Alois Kraus" + Environment.NewLine +
                             "SerializerTests [-Runs dd] -test [serialize, deserialize, combined, firstCall]" + Environment.NewLine +
                             " -Runs      Default is 5. The result is averaged where the first run is excluded from the average" + Environment.NewLine  +
                             " -test      Tests a scenario to compare the overhead of many different serializers" + Environment.NewLine +
                             "            To execute deserialize you must first have called the serialize to generate serialized test data on disk to be read during deserialize" + Environment.NewLine;
        private Queue<string> Args;

        List<ISerializeDeserializeTester> SerializersToTest;
        List<ISerializeDeserializeTester> StartupSerializersToTest;

        int Runs = 5;


        public Program(string[] args)
        {
            Args = new Queue<string>(args);
            SerializersToTest = new List<ISerializeDeserializeTester>
            {
                new Wire<BookShelf>(Data),
                new Protobuf_net<BookShelf>(Data),
                new MsgPack<BookShelf>(Data),
                new SlimSerializer<BookShelf>(Data),
                new Jil<BookShelf>(Data),
                new FastJson<BookShelf>(Data),
                new DataContractIndented<BookShelf>(Data),
                new DataContractBinaryXml<BookShelf>(Data),
                new DataContract<BookShelf>(Data),
                new XmlSerializer<BookShelf>(Data),
                new JsonNet<BookShelf>(Data),
                new BinaryFormatter<BookShelf>(Data),
            };

            StartupSerializersToTest = new List<ISerializeDeserializeTester>
            {
                new Wire<BookShelf>(Data),
                new Wire<BookShelf1>(Data1),
                new Wire<BookShelf2>(Data2),
                new Wire<LargeBookShelf>(DataLarge),

                new MsgPack<BookShelf>(Data),
                new MsgPack<BookShelf1>(Data1),
                new MsgPack<BookShelf2>(Data2),
                new MsgPack<LargeBookShelf>(DataLarge),

                new SlimSerializer<BookShelf>(Data),
                new SlimSerializer<BookShelf1>(Data1),
                new SlimSerializer<BookShelf2>(Data2),
                new SlimSerializer<LargeBookShelf>(DataLarge),

                new BinaryFormatter<BookShelf>(Data),
                new BinaryFormatter<BookShelf1>(Data1),
                new BinaryFormatter<BookShelf2>(Data2),
                new BinaryFormatter<LargeBookShelf>(DataLarge),

                new FastJson<BookShelf>(Data),
                new FastJson<BookShelf1>(Data1),
                new FastJson<BookShelf2>(Data2),
                new FastJson<LargeBookShelf>(DataLarge),

                new Jil<BookShelf>(Data),
                new Jil<BookShelf1>(Data1),
                new Jil<BookShelf2>(Data2),
                new Jil<LargeBookShelf>(DataLarge),

                new DataContract<BookShelf>(Data),
                new DataContract<BookShelf1>(Data1),
                new DataContract<BookShelf2>(Data2),
                new DataContract<LargeBookShelf>(DataLarge),

                new XmlSerializer<BookShelf>(Data),
                new XmlSerializer<BookShelf1>(Data1),
                new XmlSerializer<BookShelf2>(Data2),
                new XmlSerializer<LargeBookShelf>(DataLarge),


                new JsonNet<BookShelf>(Data),
                new JsonNet<BookShelf1>(Data1),
                new JsonNet<BookShelf2>(Data2),
                new JsonNet<LargeBookShelf>(DataLarge),

                new Protobuf_net<BookShelf>(Data),
                new Protobuf_net<BookShelf1>(Data1),
                new Protobuf_net<BookShelf2>(Data2),
                new Protobuf_net<LargeBookShelf>(DataLarge),
            };

        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return;
            }

            try
            {
                new Program(args).Run();
            }
            catch (Exception ex)
            {
                PrintHelp(ex);
            }
        }

        static void PrintHelp(Exception ex=null)
        {
            Console.WriteLine(Help);
            if( ex != null )
            {
                Console.WriteLine($"{ex.GetType().Name}: {ex.Message}");
            }
        }

        private void Run()
        {
            PreChecks();

            while (Args.Count > 0)
            {
                string curArg = Args.Dequeue();
                string lowerArg = curArg.ToLower();

                switch (lowerArg)
                {
                    case "-runs":
                        string n = NextLower();
                        Runs = int.Parse(n);
                        break;
                    case "-test":
                        string nextArg = NextLower();
                        if (nextArg?.Equals("serialize") == true)
                        {
                            Serialize();
                        }
                        else if (nextArg?.Equals("deserialize") == true)
                        {
                            Deserialize();
                        }
                        else if (nextArg?.Equals("firstcall") == true)
                        {
                            FirstCall();
                        }
                        else if(nextArg?.Equals("combined") == true)
                        {
                            Combined();
                        }
                        else
                        {
                            throw new NotSupportedException($"Error: Arg {nextArg} is not a valid option!");
                        }
                        break;
                    default:
                        throw new NotSupportedException($"Argument {curArg} is not valid");
                }
            }
        }

        private void PreChecks()
        {
            // Since XmlSerializer tries to load a pregenerated serialization assembly which will on first access read the GAC contents from the registry and cache them
            // we do this before to measure not the overhead of an failed assembly load try but only the overhead of the code gen itself.
            try
            {
                Assembly.Load("notExistingToTriggerGACPrefetch, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            }
            catch (FileNotFoundException ex)
            {
            }

            if (!IsNGenned())
            {
                Console.WriteLine( "Warning: Not NGenned! Results may not be accurate in your target deployment.");
                Console.WriteLine(@"Please execute: %windir%\Microsoft.NET\Framework64\v4.0.30319\ngen.exe install SerializerTests.exe");
                Console.WriteLine(@"This will precompile the executable and all referenced dlls. Undoing this is not as easy since you must call ngen uninstall for the executable and all references assemblies.");
            }

            WarnIfDebug();
        }


        private void Deserialize()
        {
            var tester = new Test_O_N_Behavior(SerializersToTest);
            tester.TestDeserialize(nRuns: Runs);
        }

        private void Serialize()
        {
            var tester = new Test_O_N_Behavior(SerializersToTest);
            tester.TestSerialize(nRuns: Runs);
        }

        private void Combined()
        {
            var tester = new Test_O_N_Behavior(SerializersToTest);
            tester.TestCombined(nRuns: Runs);
        }


        /// <summary>
        /// Test for each serializer 5 different types the first call effect
        /// </summary>
        private void FirstCall()
        {
            var tester = new Test_O_N_Behavior(StartupSerializersToTest);
            tester.TestSerialize(nObjects: 1, nRuns:1);
        }

        string NextLower()
        {
            if( Args.Count > 0 )
            {
                return Args.Dequeue().ToLower();
            }

            return null;
        }

        private bool IsNGenned()
        {
            bool lret = false;
            foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
            {
                string file = module.ModuleName;
                if( file == "SerializerTests.ni.exe" )
                {
                    lret = true;
                }
            }

            return lret;
        }

        [Conditional("DEBUG")]
        void WarnIfDebug()
        {
            Console.WriteLine();
            Console.WriteLine("DEBUG build detected. Please recompile in Release mode before publishing your data.");
        }


        BookShelf Data(int nToCreate)
        {
            var lret = new BookShelf("private member value");
            lret.Books = Enumerable.Range(1, nToCreate).Select(i => new Book { Id = i, Title = $"Book {i}" }).ToList();
            return lret;
        }

        BookShelf1 Data1(int nToCreate)
        {
            var lret = new BookShelf1("private member value1");
            lret.Books = Enumerable.Range(1, nToCreate).Select(i => new Book1 { Id = i, Title = $"Book {i}" }).ToList();
            return lret;
        }

        BookShelf2 Data2(int nToCreate)
        {
            var lret = new BookShelf2("private member value2");
            lret.Books = Enumerable.Range(1, nToCreate).Select(i => new Book2 { Id = i, Title = $"Book {i}" }).ToList();
            return lret;
        }

        LargeBookShelf DataLarge(int nToCreate)
        {
            var lret = new LargeBookShelf("private member value2");
            lret.Books = Enumerable.Range(1, nToCreate).Select(i => new LargeBook { Id = i, Title = $"Book {i}" }).ToList();
            return lret;
        }

    }
}
