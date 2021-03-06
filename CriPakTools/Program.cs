﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace CriPakTools
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("err no args\n");
                return;
            }

            bool doExtract = false;
            bool doReplace = false;
            bool doDisplay = false;
            string outDir = ".";
            string inFile = "";
            string outFile = "";
            string replaceMe = "";
            string replaceWith = "";

            for (int i = 0; i < args.Length; i++)
            {
                string option =  args[i];
                if (option[0] == '-')
                {
                    switch (option[1])
                    {
                        case 'x': doExtract = true; break;
                        case 'r': doReplace = true; replaceMe = args[i + 1]; replaceWith = args[i + 2]; break;
                        case 'l': doDisplay = true; break;
                        case 'd': outDir = args[i + 1]; break;
                        case 'i': inFile = args[i + 1]; break;
                        case 'o': outFile = args[i + 1]; break;
                        default:
                            Console.WriteLine("CriPakTool Usage:");
                            Console.WriteLine(" -l - Displays all contained chunks.");
                            Console.WriteLine(" -x - Extracts all files.");
                            Console.WriteLine(" -r REPLACE_ME REPLACE_WITH - Replaces REPLACE_ME with REPLACE_WITH.");
                            Console.WriteLine(" -o OUT_FILE - Set output file.");
                            Console.WriteLine(" -d OUT_DIR - Set output directory.");
                            Console.WriteLine(" -i IN_FILE - Set input file.");
                            break;
                    }
                }
             }
            if (!(doExtract || doReplace || doDisplay)) { //Lazy sanity checking for now
                Console.WriteLine("no? \n");
                return;
            }

            string cpk_name = inFile;

            CPK cpk = new CPK(new Tools());
            cpk.ReadCPK(cpk_name);

            BinaryReader oldFile = new BinaryReader(File.OpenRead(cpk_name));

            if (doDisplay)
            {
                List<FileEntry> entries = cpk.FileTable.OrderBy(x => x.FileOffset).ToList();
                for (int i = 0; i < entries.Count; i++)
                {
                    Console.WriteLine(((entries[i].DirName != null) ? entries[i].DirName + "/" : "") + entries[i].FileName);
                }
            }
            else if (doExtract)
            {

                List<FileEntry> entries = null;

                entries = cpk.FileTable.Where(x => x.FileType == "FILE").ToList();

                if (entries.Count == 0)
                {
                    Console.WriteLine("err while extracting.");
                }

                for (int i = 0; i < entries.Count; i++)
                {
                    if (!String.IsNullOrEmpty((string)entries[i].DirName))
                    {
                        Directory.CreateDirectory(outDir + "/" + entries[i].DirName.ToString());
                    }

                    oldFile.BaseStream.Seek((long)entries[i].FileOffset, SeekOrigin.Begin);
                    string isComp = Encoding.ASCII.GetString(oldFile.ReadBytes(8));
                    oldFile.BaseStream.Seek((long)entries[i].FileOffset, SeekOrigin.Begin);

                    byte[] chunk = oldFile.ReadBytes(Int32.Parse(entries[i].FileSize.ToString()));
                    if (isComp == "CRILAYLA")
                    {
                        int size = Int32.Parse((entries[i].ExtractSize ?? entries[i].FileSize).ToString());
                        if (size != 0)
                            chunk = cpk.DecompressCRILAYLA(chunk, size);
                    }

                    File.WriteAllBytes(outDir + "/" + ((entries[i].DirName != null) ? entries[i].DirName + "/" : "") + entries[i].FileName.ToString(), chunk);
                }
            }
            else
            {

                string ins_name = replaceMe;
                string replace_with = replaceWith;

                FileInfo fi = new FileInfo(cpk_name);

                string outputName = outFile;

                BinaryWriter newCPK = new BinaryWriter(File.OpenWrite(outputName));

                List<FileEntry> entries = cpk.FileTable.OrderBy(x => x.FileOffset).ToList();

                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i].FileType != "CONTENT")
                    {

                        if (entries[i].FileType == "FILE")
                        {
                            // I'm too lazy to figure out how to update the ContextOffset position so this works :)
                            if ((ulong)newCPK.BaseStream.Position < cpk.ContentOffset)
                            {
                                ulong padLength = cpk.ContentOffset - (ulong)newCPK.BaseStream.Position;
                                for (ulong z = 0; z < padLength; z++)
                                {
                                    newCPK.Write((byte)0);
                                }
                            }
                        }
                        

                        if (entries[i].FileName.ToString() != ins_name)
                        {
                            oldFile.BaseStream.Seek((long)entries[i].FileOffset, SeekOrigin.Begin);
                            
                            entries[i].FileOffset = (ulong)newCPK.BaseStream.Position;
                            cpk.UpdateFileEntry(entries[i]);

                            byte[] chunk = oldFile.ReadBytes(Int32.Parse(entries[i].FileSize.ToString()));
                            newCPK.Write(chunk);
                        }
                        else
                        {
                            byte[] newbie = File.ReadAllBytes(replace_with);
                            entries[i].FileOffset = (ulong)newCPK.BaseStream.Position;
                            entries[i].FileSize = Convert.ChangeType(newbie.Length, entries[i].FileSizeType);
                            entries[i].ExtractSize = Convert.ChangeType(newbie.Length, entries[i].FileSizeType);
                            cpk.UpdateFileEntry(entries[i]);
                            newCPK.Write(newbie);
                        }

                        if ((newCPK.BaseStream.Position % 0x800) > 0)
                        {
                            long cur_pos = newCPK.BaseStream.Position;
                            for (int j = 0; j < (0x800 - (cur_pos % 0x800)); j++)
                            {
                                newCPK.Write((byte)0);
                            }
                        }
                    }
                    else
                    {
                        // Content is special.... just update the position
                        cpk.UpdateFileEntry(entries[i]);
                    }
                }

                cpk.WriteCPK(newCPK);
                cpk.WriteITOC(newCPK);
                cpk.WriteTOC(newCPK);
                cpk.WriteETOC(newCPK);
                cpk.WriteGTOC(newCPK);

                newCPK.Close();
                oldFile.Close();

            }
        }
    }
}
