using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DynamicDevices.DiskWriter.Win32
{
	internal class Win32DiskAccess
	{
		#region Fields

		public SafeFileHandle _partitionHandle = null;
		public SafeFileHandle _diskHandle = null;

		#endregion

		#region IDiskAccess Members

		public event EventHandler OnDiskChanged;

		public void Open(string drivePath)
		{
			int intOut;

			//
			// Now that we've dismounted the logical volume mounted on the removable drive we can open up the physical disk to write
			//
			var diskHandle = NativeMethods.CreateFile(drivePath, NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE, NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE, IntPtr.Zero, NativeMethods.OPEN_EXISTING, 0, IntPtr.Zero);
			if (diskHandle.IsInvalid)
			{
				throw new Exception(@"Failed to open device: " + Marshal.GetHRForLastWin32Error());
			}

			var success = NativeMethods.DeviceIoControl(diskHandle, NativeMethods.FSCTL_LOCK_VOLUME, null, 0, null, 0, out intOut, IntPtr.Zero);
			if (!success)
			{
				diskHandle.Dispose();
				throw new Exception(@"Failed to lock device");
			}

			_diskHandle = diskHandle;
		}

		public bool LockDrive(string drivePath)
		{
			bool success;
			int intOut;
			SafeFileHandle partitionHandle;

			//
			// Unmount partition (Todo: Note that we currently only handle unmounting of one partition, which is the usual case for SD Cards)
			//

			//
			// Open the volume
			///
			partitionHandle = NativeMethods.CreateFile(@"\\.\" + drivePath, NativeMethods.GENERIC_READ, NativeMethods.FILE_SHARE_READ, IntPtr.Zero, NativeMethods.OPEN_EXISTING, 0, IntPtr.Zero);
			if (partitionHandle.IsInvalid)
			{
				partitionHandle.Dispose();
				throw new Exception(@"Failed to open device");
			}

			//
			// Lock it
			//
			success = NativeMethods.DeviceIoControl(partitionHandle, NativeMethods.FSCTL_LOCK_VOLUME, null, 0, null, 0, out intOut, IntPtr.Zero);
			if (!success)
			{
				partitionHandle.Dispose();
				throw new Exception(@"Failed to lock device");
			}

			//
			// Dismount it
			//
			success = NativeMethods.DeviceIoControl(partitionHandle, NativeMethods.FSCTL_DISMOUNT_VOLUME, null, 0, null, 0, out intOut, IntPtr.Zero);
			if (!success)
			{
				NativeMethods.DeviceIoControl(partitionHandle, NativeMethods.FSCTL_UNLOCK_VOLUME, null, 0, null, 0, out intOut, IntPtr.Zero);
				partitionHandle.Dispose();
				throw new Exception(@"Error dismounting volume: " + Marshal.GetHRForLastWin32Error());
			}

			_partitionHandle = partitionHandle;

			return true;
		}


		public void UnlockDrive()
		{
			if (_partitionHandle != null)
			{
				_partitionHandle.Dispose();
				_partitionHandle = null;
			}
		}

		public int Read(byte[] buffer, int readMaxLength, out int readBytes)
		{
			readBytes = 0;

			if (_diskHandle == null)
				return -1;

			return NativeMethods.ReadFile(_diskHandle, buffer, readMaxLength, out readBytes, IntPtr.Zero);
		}

		public int Write(byte[] buffer, int bytesToWrite, out int wroteBytes)
		{
			wroteBytes = 0;
			if (_diskHandle == null)
				return -1;

			return NativeMethods.WriteFile(_diskHandle, buffer, bytesToWrite, out wroteBytes, IntPtr.Zero);
		}

		public void Close()
		{
			if (_diskHandle != null)
			{
				_diskHandle.Dispose();
				_diskHandle = null;
			}
		}
	}
}
#endregion