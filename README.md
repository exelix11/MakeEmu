# MakeEmu
A simple tool to flash sd cards for Atmosphère's emummc on Windows

As now hekate supports creating the emummc partition directly it's recommended to use it instead, if you already have a nand backup on your pc this should still work but you may not receive support in case of issues. 

## Warning
**This program directly writes to disks and so it can corrupt partition tables and cause data loss, use it at your own risk**


This tool is meant for people who already know what to do and just need an easy way of flashing the sd on Windows, if you're clueless please wait for a proper guide. 

## Usage:
1) Make two FAT32 partitions on your sd card, one for your data and one for the emummc, the emummc one has to be at least 29.3 GB.
2) Make a boot0, boot1 and rawnand backup in CTCaer hekate
3) Run `MakeEmu boot0 boot1 rawnand.bin <Letter of the 29.3 GB partition>`
4) Wait
5) Profit

This wasn't originally meant to be public but someone asked, it may not receive further updates

## Credits
Some code for flashing drives was taken from [DynamicDevices/DiskImager](https://github.com/DynamicDevices/DiskImager)
