I forgot the password to my Raspberry Pi. The Pi runs headless (no monitor or keyboard attached), so I couldn't just boot into recovery mode. The typical solution - mounting the SD card on another Linux machine and editing files directly - didn't work because Windows 11 refused to mount the ext4 partition.

The workaround: create an image of the SD card, mount it in WSL, edit the password hash, and write the image back.

## The Problem

The Raspberry Pi OS stores user passwords in `/etc/shadow`, just like any Linux system. To reset a password without booting the Pi, you need to:

1. Access the SD card's filesystem
2. Edit `/etc/shadow` to set a new password hash
3. Boot the Pi with the modified card

Simple on a Linux machine. Not simple on Windows.

## Why Windows Can't Mount It

SD cards for Raspberry Pi typically have two partitions:

- **boot** - FAT32, readable by Windows
- **rootfs** - ext4, Linux filesystem

Windows doesn't natively support ext4. There are third-party tools like Ext2Fsd, but on Windows 11 they often fail with driver signing issues or BSOD risks.

When I inserted the SD card, Windows saw the boot partition but threw "You need to format the disk" errors for the rootfs partition. Clicking through those dialogs would have destroyed the filesystem.

## The WSL Approach

Windows Subsystem for Linux (WSL) runs a real Linux kernel that can mount ext4 filesystems. The trick is getting WSL to see the SD card.

WSL2 doesn't automatically pass through USB devices or SD card readers. But it can mount disk image files.

### Step 1: Create a Disk Image

First, create a raw image of the entire SD card using a tool that can access the physical device. I used [Win32 Disk Imager](https://sourceforge.net/projects/win32diskimager/):

1. Insert the SD card
2. Open Win32 Disk Imager as Administrator
3. Select the SD card device (be careful - select the right one)
4. Choose an output file path (e.g., `C:\temp\pi-backup.img`)
5. Click "Read" to create the image

This creates a bit-for-bit copy of the SD card, including all partitions.

### Step 2: Mount the Image in WSL

Open your WSL distribution (Ubuntu, Debian, etc.) and mount the image:

```bash
# Create a mount point
sudo mkdir -p /mnt/piroot

# Find the offset of the rootfs partition
# The boot partition is first, rootfs is second
fdisk -l /mnt/c/temp/pi-backup.img
```

The `fdisk` output shows something like:

```
Device                    Boot   Start      End  Sectors  Size Id Type
/mnt/c/temp/pi-backup.img1        8192   532479   524288  256M  c W95 FAT32 (LBA)
/mnt/c/temp/pi-backup.img2      532480 62333951 61801472 29.5G 83 Linux
```

The rootfs partition starts at sector 532480. With 512-byte sectors, the offset is:

```
532480 * 512 = 272629760
```

Now mount with that offset:

```bash
sudo mount -o loop,offset=272629760 /mnt/c/temp/pi-backup.img /mnt/piroot
```

You should now see the Pi's filesystem at `/mnt/piroot`.

### Step 3: Generate a New Password Hash

Linux password hashes in `/etc/shadow` use a specific format. Generate one for your new password:

```bash
openssl passwd -6 -salt $(openssl rand -base64 8) "yournewpassword"
```

The `-6` flag specifies SHA-512 hashing (the modern standard). This outputs something like:

```
$6$randomsalt$longhashstring...
```

Copy this entire string.

### Step 4: Edit the Shadow File

Open the shadow file:

```bash
sudo nano /mnt/piroot/etc/shadow
```

Find the line for your user (typically `pi` or whatever username you created):

```
pi:$6$oldhashherexxxxxxx:19000:0:99999:7:::
```

Replace the hash (the part between the first and second colons after the username) with your new hash:

```
pi:$6$randomsalt$longhashstring...:19000:0:99999:7:::
```

Save and exit.

### Step 5: Unmount and Write Back

```bash
sudo umount /mnt/piroot
```

Back in Windows, use Win32 Disk Imager again:

1. Select the SD card device
2. Select the modified image file
3. Click "Write" to write the image back to the card

### Step 6: Boot and Test

Insert the SD card into the Pi, boot it up, and SSH in with your new password.

## Alternative: Clear the Password

Instead of setting a new password, you can clear it entirely and set one on first login:

In `/etc/shadow`, replace the hash with an empty field:

```
pi::19000:0:99999:7:::
```

The double colon means no password. You'll be able to log in without a password (dangerous - do this only temporarily) or you'll be prompted to set one.

A safer option is to use a single `*` which locks the account but allows passwordless `sudo` if configured:

```
pi:*:19000:0:99999:7:::
```

## Why Not Just Reflash?

You could reflash the SD card with a fresh Raspberry Pi OS image. But then you lose:

- All installed packages and configurations
- Cron jobs and custom scripts
- Service configurations (in my case, Certbot and its certificates)
- Any data stored on the Pi

The image-and-edit approach preserves everything.

## Lessons Learned

- **Win32 Disk Imager** is still the most reliable tool for SD card imaging on Windows
- **WSL2 can mount disk images** even though it can't directly access USB devices
- **Sector offsets matter** - `fdisk -l` tells you where partitions start
- **Keep a backup** - I now keep a periodic image of my Pi's SD card
- **Write down passwords** - or use a password manager

The whole process took about 30 minutes, most of which was waiting for the image to read and write. Faster than reinstalling and reconfiguring everything.
