﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Nintendo_Relocatable_Module_Decoder
{
    class Program
    {
        private static int REL_HEADER_SIZE = 0xA0;
        private static int DOL_HEADER_SIZE = -0xC0;
        private static int CURRENT_HEADER_SIZE;

        private static Dictionary<string, int> Data_Section_Map;

        static void Main(string[] args)
        {
            Console.WriteLine("Enter the path to the Relocatable Module file (.rel):");
            string REL_Location = Console.ReadLine();
            if (File.Exists(REL_Location))
            {
                Console.WriteLine("Enter the path to the Relocatable Module Map file (.map):");
                string MAP_Location = Console.ReadLine();
                if (File.Exists(MAP_Location))
                {
                    CURRENT_HEADER_SIZE = Path.GetExtension(REL_Location).Contains("dol") ? DOL_HEADER_SIZE : REL_HEADER_SIZE;
                    byte[] REL_Data = File.ReadAllBytes(REL_Location);
                    string[] MAP_Data = File.ReadAllLines(MAP_Location);
                    string Memory_Map = MAP_Data.FirstOrDefault(o => o.Contains("Memory map:"));
                    if (!string.IsNullOrEmpty(Memory_Map))
                    {
                        int Memory_Map_Idx = Array.IndexOf(MAP_Data, Memory_Map);
                        if (Memory_Map_Idx > -1)
                        {
                            Memory_Map_Idx += 3; // Data starts three lines after
                            string Data_Dir = Path.GetDirectoryName(REL_Location) + "\\" + Path.GetFileNameWithoutExtension(REL_Location);
                            if (!Directory.Exists(Data_Dir))
                            {
                                Directory.CreateDirectory(Data_Dir);
                            }

                            // Create Section Folders
                            Data_Section_Map = new Dictionary<string, int>();
                            for (int i = Memory_Map_Idx; i < MAP_Data.Length; i++)
                            {
                                if (string.IsNullOrEmpty(MAP_Data[i]))
                                    break;
                                string Section_Info = MAP_Data[i].TrimStart();
                                string Section_Name = Regex.Match(Section_Info, @"^[^ ]*").Value;
                                string Section_Offsets = Section_Info.Substring(Section_Name.Length + 11);
                                string Section_Size = Regex.Match(Section_Offsets, @"^[^ ]*").Value;
                                string Section_Offset = Section_Offsets.Substring(Section_Size.Length + 1, 8);
                                if (int.TryParse(Section_Offset, NumberStyles.AllowHexSpecifier, null, out int Offset)
                                    && int.TryParse(Section_Size, NumberStyles.AllowHexSpecifier, null, out int Size))
                                {
                                    string Section_Dir = Data_Dir + "\\" + Section_Name;
                                    if (!Directory.Exists(Section_Dir))
                                    {
                                        Directory.CreateDirectory(Section_Dir);
                                    }
                                    Data_Section_Map.Add(Section_Name, Offset);
                                }
                            }

                            // Section off data
                            string Current_Section = "";
                            for (int i = 0; i < Memory_Map_Idx - 3; i++)
                            {
                                string Line = MAP_Data[i];
                                if (!string.IsNullOrEmpty(Line))
                                {
                                    if (Line.Contains(" section layout"))
                                    {
                                        i += 3; // Skip column text
                                        Current_Section = Regex.Match(Line.TrimStart(), @"^[^ ]*").Value;
                                        Console.WriteLine("Switched to section: " + Current_Section);
                                        //Console.ReadKey();
                                    }
                                    else if (!string.IsNullOrEmpty(Current_Section))
                                    {
                                        if (Line.Contains(@"..."))
                                        {
                                            //Console.WriteLine("Contained ... : " + Line);
                                            continue;
                                        }

                                        Line = Line.Trim(); // Clear Leading/Trailing Whitespace
                                        Line = Line.Replace("\t", " "); // Confirm all tabs get turned into a space
                                        Line = Regex.Replace(Line, @"\s+", " "); // Turn multiple spaces/tabs to one space
                                        string[] Line_Data = Line.Split(' ');
                                        /*
                                         * Line_Data contents
                                         * =================
                                         * Starting Address (relative to section start) 0
                                         * Size 1
                                         * Virtual Address (same as Starting Address??) 2
                                         * Type (1 = Object, 4 = Method) 3
                                         * Name 4
                                         * Object 5
                                         */
                                        int Offset = GetRELOffset(Data_Section_Map[Current_Section], int.Parse(Line_Data[1], NumberStyles.AllowHexSpecifier));
                                        int Size = int.Parse(Line_Data[1], NumberStyles.AllowHexSpecifier);
                                        bool IsObject = Line_Data[3].Equals("1");
                                        string Method_Name = Line_Data[4];
                                        string Object_Name = Line_Data[5];
                                        if (Line_Data.Length >= 7)
                                            Object_Name += ("_" + Line_Data[6]);

                                        string Dir = Data_Dir + "\\" + Current_Section + "\\" + Object_Name;
                                        if (!Directory.Exists(Dir))
                                        {
                                            Directory.CreateDirectory(Dir);
                                        }
                                        
                                        if (!IsObject)
                                        {
                                            try
                                            {
                                                using (FileStream Data_File = File.Create(Dir + "\\" + Method_Name + ".bin"))
                                                {
                                                    Data_File.Write(REL_Data, Offset, Size);
                                                    Data_File.Flush();
                                                }
                                            }
                                            catch
                                            {
                                                //Console.WriteLine(string.Format("Unable to create file for: {0}/{1}! Offset was past the end of the file!",
                                                    //Current_Section, Object_Name));
                                                //Console.ReadKey();
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine(string.Format("Parsing data for {0}/{1}", Current_Section, Object_Name));
                                        }
                                    }
                                }
                            }
                            Console.WriteLine("Finished parsing the relocatable module! Press any key to close this window.");
                            Console.ReadKey();
                        }
                    }


                }
            }
        }

        private static int GetRELOffset(int Section_Offset, int Data_Offset)
        {
            return CURRENT_HEADER_SIZE + Section_Offset + Data_Offset; // Header is 0xA0 bytes
        }

    }
}
