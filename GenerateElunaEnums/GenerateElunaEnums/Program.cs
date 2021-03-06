﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Configuration;
using System.Diagnostics;
using System.Web.Script.Serialization;
using GenerateElunaEnums.Helpers;
using GenerateElunaEnums.Classes;

namespace GenerateElunaEnums
{
   static class Program
   {
      static void Main(string[] args)
      {
         string hooksHeader = ElunaHooksHelper.GetHooksHeader();
         List<LuaEnum> parsedEnums = ElunaHooksHelper.ParseHooksHeader(hooksHeader);
         List<Map> maps = GetMaps();
         Dictionary<Map.InstanceTypes, List<Map>> mapDict = MapHelper.CreateMapDictionary(maps);
         GenerateLuaFile(parsedEnums, mapDict);
      }

      private static List<Map> GetMaps()
      {
         string dbcFolderPath = ConfigurationManager.AppSettings["DBCFolderPath"];
         string wdbxEditorPath = ConfigurationManager.AppSettings["WDBXEditorPath"];
         string workingDirectory = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
         // Build 5875 = 1.12.1
         string arguments = $"-export -f \"{dbcFolderPath}\\Map.dbc\" -b 5875 -o \"{workingDirectory}\\Map.json\"";
         Process process = new Process();
         ProcessStartInfo startInfo = new ProcessStartInfo();
         //startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
         startInfo.FileName = $"{wdbxEditorPath}\\WDBX Editor.exe";
         startInfo.Arguments = arguments;
         process.StartInfo = startInfo;
         process.Start();
         Console.WriteLine($"{startInfo.FileName} {startInfo.Arguments}");
         if (!process.WaitForExit(6000)) // wait up to 1 minute
            throw new TimeoutException("WDBX Editor did not exit after 1 minute.");
         List<Map> maps = new JavaScriptSerializer().Deserialize<List<Map>>(File.ReadAllText("Map.json"));
         return maps;
      }

      private static void GenerateLuaFile(List<LuaEnum> parsedEnums, Dictionary<Map.InstanceTypes, List<Map>> mapDict)
      {
         string outputPath = ConfigurationManager.AppSettings["OutputPath"];
         using (StreamWriter file = new StreamWriter($"{outputPath}\\Constants.lua"))
         {
            file.WriteLine(@"-- Generated by GenerateElunaEnums on " + System.DateTime.Now);
            // Add the declaration for readOnlyTables
            string readOnlyTable = @"function readOnlyTable(table)"                                   + Environment.NewLine +
                                   @"  return setmetatable({}, {"                                     + Environment.NewLine +
                                   @"    __index = table,"                                            + Environment.NewLine +
                                   @"    __newindex = function(table, key, value)"                    + Environment.NewLine +
                                   @"                   error(""Attempt to modify read-only table"")" + Environment.NewLine +
                                   @"                 end,"                                           + Environment.NewLine +
                                   @"    __metatable = false"                                         + Environment.NewLine +
                                   @"  }); "                                                          + Environment.NewLine +
                                   @"end"                                                             + Environment.NewLine + Environment.NewLine;
            file.Write(readOnlyTable);
            foreach (LuaEnum hookEnum in parsedEnums)
            {
               file.WriteLine($"{hookEnum}{System.Environment.NewLine}");
            }
            
            foreach (Map.InstanceTypes instanceType in Enum.GetValues(typeof(Map.InstanceTypes)))
            {
               List<Map> maps = mapDict[instanceType];
               file.WriteLine($"{Enum.GetName(typeof(Map.InstanceTypes),instanceType)}Maps = readOnlyTable {{");
               foreach (Map map in maps)
               {
                  file.WriteLine($"   {map}");
               }
               file.WriteLine($"}}{System.Environment.NewLine}");
            }
         }
      }
   }
}
