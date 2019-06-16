# MakeEmu
A simple tool to flash sd cards for Atmosphère's emummc on Windows

## Warning
**This program directly writes to disks and so it can corrupt partition tables and cause data loss, use it at your own risk**

## Usage:
1) Make two FAT32 partitions on your sd card, one for your data and one for the emummc, the emummc one has to be at least 29.3 GB.
2) Make a boot0, boot1 and rawnand backup in CTCaer hekate
3) Run `MakeEmu boot0 boot1 rawnand.bin <Letter of the 29.3 GB partition>`
4) Wait
5) Profit

## Credits
Some code for flashing drives was taken from [DynamicDevices/DiskImager](https://github.com/DynamicDevices/DiskImager)
