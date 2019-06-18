using DynamicDevices.DiskWriter;
using DynamicDevices.DiskWriter.Win32;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MakeEmu
{
	class Program
	{
		const uint ENABLE_QUICK_EDIT = 0x0040;
		const int STD_INPUT_HANDLE = -10;
		[DllImport("kernel32.dll", SetLastError = true)]
		static extern IntPtr GetStdHandle(int nStdHandle);

		[DllImport("kernel32.dll")]
		static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

		[DllImport("kernel32.dll")]
		static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

		[DllImport("kernel32", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern int GetDiskFreeSpace(
			string lpRootPathName, out int lpSectorsPerCluster, out int lpBytesPerSector,
			out int lpNumberOfFreeClusters, out int lpTotalNumberOfClusters);

		const UInt64 ExpectedFullBackupSize = 31276924928UL;

		static void Main(string[] args)
		{
			//this disables selecting in the cmd window, an user can accidentally click on the window causing it to go to selection mode and freeze the process.
			IntPtr consoleHandle = GetStdHandle(STD_INPUT_HANDLE);
			uint consoleMode;
			GetConsoleMode(consoleHandle, out consoleMode);
			consoleMode &= ~ENABLE_QUICK_EDIT;
			SetConsoleMode(consoleHandle, consoleMode);

			Console.WriteLine("MakeEmu 1.1 --- https://github.com/exelix11/MakeEmu");
			Console.WriteLine("A simple tool to flash sd cards for Atmosphère's emummc on Windows");
			Console.WriteLine("");

			Console.WriteLine("This tool, if misused, can easily corrupt disks and cause data loss.\nBy continuing you agree to use this program at your own risk.\n");

			if (args.Length != 4)
			{
				Console.WriteLine("Usage:\nMakeEmu Boot0 Boot1 Rawnand.bin TargetDisk\nAvailable disks:");
				System.IO.DriveInfo.GetDrives().ToList().ForEach(x =>
				{
					if (x.IsReady) Console.WriteLine($"{x.Name} : {x.VolumeLabel} {x.TotalSize} Bytes");
					else Console.WriteLine($"{x.Name} : unknown filesystem");
				});
				Console.ReadKey();
				return;
			}

			string Boot0 = args[0];
			string Boot1 = args[1];
			string RawNand = args[2];
			string TargetDevice = args[3];

			if (TargetDevice.Length == 1 && char.IsLetter(TargetDevice[0]))
			{
				Console.WriteLine($"\"{TargetDevice}\" is invalid, did you mean \"{TargetDevice}:\" ? ");
				return;
			}
			
			{
				//Check device info
				GetDiskFreeSpace(TargetDevice, out int lpSectorsPerCluster, out int lpBytesPerSector, out _, out int lpTotalNumberOfClusters);
				var TotalDiskSize = (UInt64)lpTotalNumberOfClusters * (UInt64)lpSectorsPerCluster * (UInt64)lpBytesPerSector;

				if (TotalDiskSize == 0)
					Console.WriteLine($"Warning: Windows reports the disk size to be 0, this is normal if {TargetDevice} does not have a valid filesystem.\n");
				else if (TotalDiskSize < ExpectedFullBackupSize)
				{
					Console.WriteLine($"Warning: There seems to not be enough space for emunand, did you select the correct disk ?");
					Console.WriteLine("Press enter to continue anyway but the process could fail.\n");
					Console.ReadLine();
				}
			}

			if (TargetDevice.EndsWith("\\") || TargetDevice.EndsWith("/"))
				TargetDevice = TargetDevice.Substring(0, TargetDevice.Length - 1);

			var disk = new Win32DiskAccess();
			disk.Open(@"\\.\" + TargetDevice);

			int SectorSize = 0;

			{
				var geometrySize = Marshal.SizeOf(typeof(DiskGeometryEx));
				var geometryBlob = Marshal.AllocHGlobal(geometrySize);
				uint numBytesRead = 0;

				var success = NativeMethods.DeviceIoControl(disk._diskHandle, NativeMethods.IOCTL_DISK_GET_DRIVE_GEOMETRY_EX, IntPtr.Zero,
														0, geometryBlob, (uint)geometrySize, ref numBytesRead, IntPtr.Zero);

				var geometry = (DiskGeometryEx)Marshal.PtrToStructure(geometryBlob, typeof(DiskGeometryEx));

				if (success)
				{
					if (geometry.Geometry.BytesPerSector != 512)
						Console.WriteLine($"Warning: the disk BytesPerSector value is not 512");
					SectorSize = geometry.Geometry.BytesPerSector;
				}
				else
				{
					Console.WriteLine("ERROR: coulnd't get the sector size of the target device. Press enter to continue anyway...");
					Console.ReadLine();
				}

				Marshal.FreeHGlobal(geometryBlob);
			}

			var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_LogicalDiskToPartition");
			UInt64 PartitionStartingAddress = 0;
			foreach (var queryObj in searcher.Get())
			{
				if (queryObj["Dependent"].ToString().Contains($"DeviceID=\"{TargetDevice}\""))
					PartitionStartingAddress = (UInt64)queryObj["StartingAddress"];
			}

			if (PartitionStartingAddress == 0)
			{
				Console.WriteLine("ERROR: Failed to get the partition start offset. Press enter to continue anyway...");
				Console.ReadLine();
			}
			else
			{
				if (SectorSize == 0)
					Console.WriteLine("Warning: couldn't calculate the emmc_sector value as the sector size returned 0, assuming it is 512: 0x" + (PartitionStartingAddress / 512UL).ToString("X") + "\n");
				else
					Console.WriteLine("emummc_sector is 0x" + (PartitionStartingAddress / (UInt64)SectorSize).ToString("X") );
				Console.WriteLine($"(Partition starting offset is 0x{PartitionStartingAddress.ToString("X")} bytes and the sector size is {SectorSize})");
			}

			Int64 TotalSize = new FileInfo(Boot0).Length + new FileInfo(Boot1).Length + new FileInfo(RawNand).Length;

			if ((UInt64)TotalSize != ExpectedFullBackupSize)
				Console.WriteLine($"Warning: the total backup size is {TotalSize} bytes but {ExpectedFullBackupSize} is expected");

			Console.WriteLine($"\nThe total nand size is {TotalSize} and the target device is {TargetDevice}.");
			Console.WriteLine("Everything is ready, press enter to start....");
			Console.ReadLine();

			const int ReadBlockSize = 1024 * 1204 * 16; //16MB

			void WriteFileToDisk(string fileName)
			{
				Console.WriteLine("Writing file: " + fileName);
				Console.Write("0%");

				int ReadCount = 0;
				byte[] Block = new byte[ReadBlockSize];
				FileStream ifStream = new FileStream(fileName, FileMode.Open);

				while ((ReadCount = ifStream.Read(Block, 0, ReadBlockSize)) > 0)
				{
					disk.Write(Block, ReadCount, out int WriteCount);
					if (WriteCount != ReadCount)
					{
						Console.WriteLine("\nError while writing\n");
						disk.Close();
						return;
					}
					var Percent = (int)((float)ifStream.Position / ifStream.Length * 100);
					Console.SetCursorPosition(0, Console.CursorTop);
					Console.Write(Percent + "%");
				}

				Console.WriteLine("  ----  Done !");
			}

			Console.WriteLine("\n");
			WriteFileToDisk(Boot0);
			WriteFileToDisk(Boot1);
			WriteFileToDisk(RawNand);

			disk.Close();
			Console.WriteLine("Completed !");
			Console.ReadLine();
		}
	}
}
