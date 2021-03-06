﻿using Olive;
using Olive.Entities.Replication;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OliveGenerator
{
    class Context
    {
        public static string EndpointName, NugetServer, NugetApiKey;
        public static FileInfo AssemblyFile;
        public static DirectoryInfo TempPath, Output, Source;
        public static Assembly AssemblyObject;
        public static Type EndpointType;
        public static List<ExposedType> ExposedTypes = new List<ExposedType>();

        internal static void PrepareOutputDirectory()
        {
            if (!TempPath.Exists)
                throw new Exception("Output directory not found: " + TempPath.FullName);

            try
            {
                if (TempPath.Exists)
                    TempPath.DeleteAsync(recursive: true, harshly: true).WaitAndThrow();
                TempPath.Create();
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to delete the previous output directory " +
                    TempPath.FullName + Environment.NewLine + ex.Message);
            }
        }

        internal static string Run(string command)
        {
            var cmd = new Process();
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.CreateNoWindow = false;
            cmd.StartInfo.UseShellExecute = false;
            cmd.Start();
            cmd.StandardInput.WriteLine(command);
            cmd.StandardInput.Flush();
            cmd.StandardInput.Close();
            var result = cmd.StandardOutput.ReadToEnd().ToStringOrEmpty().Trim();

            if (result.StartsWith("Could not ")) throw new Exception(result);

            if (result.Contains("Build FAILED"))
            {
                Console.WriteLine("Compile " + command + " manually...");
                Console.ReadLine();
                // throw new Exception(result.TrimBefore("Build FAILED"));
            }

            return result;
        }

        internal static void LoadAssembly()
        {
            AppDomain.CurrentDomain.ResolveAssemblies();
            AssemblyObject = Assembly.LoadFrom(AssemblyFile.ExistsOrThrow().FullName);

            EndpointType = AssemblyObject.GetType(EndpointName) ??
                AssemblyObject.GetTypes().FirstOrDefault(x => x.Name == EndpointName) ??
                throw new Exception($"No type in the assembly {AssemblyFile.FullName} is named: {EndpointName}.");

            EndpointName = EndpointType.FullName; // Ensure it has full namespace 
        }

        internal static void FindExposedTypes()
        {
            ExposedTypes = EndpointType.CreateInstance<SourceEndpoint>()
                .GetTypes()
                .Select(x => x.CreateInstance<ExposedType>())
                .ToList();

            if (ExposedTypes.None()) throw new Exception("This endpoint has no exposed data types.");

            ExposedTypes.Do(x => x.Define());
        }
    }
}