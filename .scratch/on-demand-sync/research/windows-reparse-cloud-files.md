# Windows Reparse Points and Cloud Files On-Demand: Evaluation for SnapSync

**Contract Path:** `.scratch/on-demand-sync/research/windows-reparse-cloud-files.md`  
**Date:** 2026-10-01  
**Scope:** Investigation of Windows reparse points, OneDrive Files On-Demand, Dropbox, and Syncthing to evaluate SnapSync's blanket rule skipping all items where `(Attributes & FileAttributes.ReparsePoint) != 0`.

---

## 1. Executive Summary: Safety of the Blanket Skip Rule

**Finding:** The blanket rule `skip every item with FileAttributes.ReparsePoint` is **fundamentally broken and overbroad** for cloud-backed synchronization scenarios.

1. **Reparse points are not synonymous with symbolic links or junctions:**
   In Windows NTFS, a reparse point is a generic file system extension mechanism identified by a 32-bit `ReparseTag`. While symbolic links and junctions are reparse points, many other file system features—including **OneDrive Files On-Demand placeholders**, **Dropbox (Cloud Files API) placeholders**, deduplication, hierarchical storage, and project file systems—are also reparse points.
2. **Every online-only cloud placeholder carries `FileAttributes.ReparsePoint`:**
   When OneDrive Files On-Demand or modern Dropbox presents an unhydrated (online-only) file or virtualized directory to an application in default exposed mode, Windows marks that item with `FILE_ATTRIBUTE_REPARSE_POINT` (0x400) and `FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS` (0x400000) or `FILE_ATTRIBUTE_OFFLINE` (0x1000).
3. **Catastrophic Impact on SnapSync:**
   - **If OneDrive/Dropbox is the Source:** A blanket skip on `ReparsePoint` causes SnapSync to silently skip **all online-only cloud files and virtualized cloud subdirectories**, resulting in partial backups or omitted data.
   - **If OneDrive/Dropbox is the Destination:** Any file mirrored into a cloud folder that has been dehydrated by the sync engine will be treated as an alien link on subsequent scan passes and potentially skipped or deleted depending on sync logic.
4. **Hydration behavior:**
   Standard read/open calls (`File.OpenRead`, `File.ReadAllBytes`, `FileStream`) without remote-storage flags cause `cldflt.sys` to automatically recall/download the file over the network. If SnapSync intends to mirror content, opening will hydrate; if SnapSync intends to avoid forced network downloads, checking `FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS` / `FILE_ATTRIBUTE_OFFLINE` is required rather than skipping `ReparsePoint`.

---

## 2. Windows Cloud Files Architecture & Attributes

### 2.1 Cloud Filter Driver (`cldflt.sys`) & Cloud Files API
Starting in Windows 10 version 1709 (Fall Creators Update), Microsoft formalized cloud sync engines via the **Cloud Files API** and the Windows kernel minifilter driver `cldflt.sys` (CfApi).

- **Primary Source:** Microsoft Learn, *Build a Cloud Sync Engine that Supports Placeholder Files*  
  [https://learn.microsoft.com/en-us/windows/win32/cfapi/build-a-cloud-file-sync-engine](https://learn.microsoft.com/en-us/windows/win32/cfapi/build-a-cloud-file-sync-engine)
- **Primary Source:** Microsoft Learn, *Cloud Filter API Reference*  
  [https://learn.microsoft.com/en-us/windows/win32/cfapi/cloud-files-api-portal](https://learn.microsoft.com/en-us/windows/win32/cfapi/cloud-files-api-portal)

Microsoft documentation explicitly states:
> *"The cloud files API implements the placeholder system using reparse points. A common misconception about reparse points is that they are the same as symbolic links. This misconception is occasionally reflected in application implementations, and as a result, many existing applications hit errors when encountering any reparse point."*

### 2.2 Win32 & .NET Attributes for Cloud Files
When a file is managed by a cloud sync engine (such as OneDrive or Dropbox), its attributes reflect its presence state:

| Attribute Constant | Bit Value | .NET Enum Support | Meaning in Cloud Sync Engine |
| :--- | :--- | :--- | :--- |
| `FILE_ATTRIBUTE_REPARSE_POINT` | `0x00000400` (`1024`) | `FileAttributes.ReparsePoint` | Present on all placeholder files and virtualized placeholder directories. |
| `FILE_ATTRIBUTE_OFFLINE` | `0x00001000` (`4096`) | `FileAttributes.Offline` | Traditional HSM offline flag; set on dehydrated/online-only files. |
| `FILE_ATTRIBUTE_SPARSE_FILE` | `0x00000200` (`512`) | `FileAttributes.SparseFile` | Placeholder files use NTFS sparse allocation to occupy minimal disk space (e.g. 1 KB header). |
| `FILE_ATTRIBUTE_PINNED` | `0x00080000` (`524288`) | Not in standard `FileAttributes` (use raw int/cast) | User intent: "Always keep on this device". |
| `FILE_ATTRIBUTE_UNPINNED` | `0x00100000` (`1048576`) | Not in standard `FileAttributes` (use raw int/cast) | User intent: "Free up space" / online only. |
| `FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS` | `0x00400000` (`4194304`) | Not in standard `FileAttributes` (use raw int/cast) | File content is not fully present locally; reading it triggers network retrieval. |
| `FILE_ATTRIBUTE_RECALL_ON_OPEN` | `0x00040000` (`262144`) | Not in standard `FileAttributes` (use raw int/cast) | Appears in directory enumeration for virtualized items requiring recall upon open. |

- **Primary Source:** Microsoft Learn, *[MS-FSCC]: File Attributes*  
  [https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-fscc/ca28ec38-f155-4768-81d6-4bfeb8586fc9](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-fscc/ca28ec38-f155-4768-81d6-4bfeb8586fc9)

---

## 3. Cloud Provider Behaviors (First-Party Evidence)

### 3.1 Microsoft OneDrive
- **Implementation:** OneDrive uses Windows Cloud Files API (`cldflt.sys`).
- **File States:**
  1. *Online-only (Dehydrated placeholder):* Contains `FileAttributes.ReparsePoint`, `FileAttributes.SparseFile`, `FileAttributes.Offline`, and `FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS` (`0x00400000`). Reparse tag is one of the `IO_REPARSE_TAG_CLOUD_*` family (`0x9000001A` through `0x9000F01A`).
  2. *Locally available (Hydrated full file):* Downloaded to local disk upon access or cached. Reparse point is often retained or removed depending on sync engine state, but `FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS` is cleared.
  3. *Always available (Pinned full file):* Has `FILE_ATTRIBUTE_PINNED` (`0x00080000`); guaranteed local copy.
- **Hydration on Open:**
  Opening an online-only OneDrive file using standard Win32 `CreateFile` (or .NET `File.OpenRead`, `new FileStream(...)`) triggers automatic hydration. `cldflt.sys` blocks the calling thread while communicating with the OneDrive sync client to download file blocks over the network. If network is disconnected, the open call fails with `ERROR_FILE_OFFLINE` (`0x80071128` or win32 error code 4350).

### 3.2 Dropbox for Windows
- **Implementation:** Modern Dropbox for Windows (since the 2022–2024 architecture update) transitioned from proprietary file system hooks to Microsoft's native **Cloud Files API**.
- **Primary Source:** Dropbox Help, *Expected changes with Dropbox for Windows update*  
  [https://help.dropbox.com/installs/windows-support-for-expected-changes](https://help.dropbox.com/installs/windows-support-for-expected-changes)  
  Dropbox confirms that sync icons, status columns, and placeholder handling are directly governed by the Windows Cloud Files API.
- **Behavior:** Online-only Dropbox files exhibit identical reparse point semantics, tags (`IO_REPARSE_TAG_CLOUD_*`), and hydration triggers as OneDrive. Skipping reparse points will skip unhydrated Dropbox files.

### 3.3 Syncthing
- **Implementation:** Syncthing is a peer-to-peer sync engine that **does not use the Windows Cloud Files API** and does not create virtual placeholder files.
- **Primary Source:** Syncthing Documentation, *FAQ - What things are synced?*  
  [https://docs.syncthing.net/users/faq.html](https://docs.syncthing.net/users/faq.html)
- **Symlink/Reparse behavior on Windows:**
  Syncthing historically disables symlink synchronization by default on Windows or requires administrative privileges / Developer Mode. When Syncthing syncs files on Windows, they are regular NTFS files on disk. Syncthing does not assign `IO_REPARSE_TAG_CLOUD_*`. However, if a user points Syncthing at a directory containing NTFS junctions or symlinks, Syncthing treats them either as ignored or errors depending on configuration.
- **Interaction with SnapSync:** Syncthing's normal files will *not* carry `FileAttributes.ReparsePoint`. However, if a user syncs a folder containing junctions or links, those junctions will carry `FileAttributes.ReparsePoint`.

---

## 4. Reparse Tags: Distinguishing Links from Cloud Placeholders

In Windows NTFS, the reparse tag is a 32-bit unsigned integer stored in the reparse point header.

- **Primary Source:** Microsoft Learn, *[MS-FSCC] 2.1.2.1 Reparse Tags*  
  [https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-fscc/c8e77b37-3909-4fe6-a4ea-2b9d423b1ee4](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-fscc/c8e77b37-3909-4fe6-a4ea-2b9d423b1ee4)
- **Primary Source:** Microsoft Learn, *Reparse Point Tags (Win32 apps)*  
  [https://learn.microsoft.com/en-us/windows/win32/fileio/reparse-point-tags](https://learn.microsoft.com/en-us/windows/win32/fileio/reparse-point-tags)

### 4.1 Tag Bit Layout
```
 3 3 2 2 2 2 2 2 2 2 2 2 1 1 1 1 1 1 1 1 1 1
 1 0 9 8 7 6 5 4 3 2 1 0 9 8 7 6 5 4 3 2 1 0 9 8 7 6 5 4 3 2 1 0
+-+-+-+-+-----------------------+-------------------------------+
|M|R|N|D|     Reserved bits     |      Reparse tag value        |
+-+-+-+-+-----------------------+-------------------------------+
```
- **Bit 31 (`M` - `0x80000000`):** Microsoft owned tag.
- **Bit 29 (`N` - `0x20000000`):** **Name Surrogate bit**.
  - Defined in `ntifs.h` via `IsReparseTagNameSurrogate(tag)`: `((tag & 0x20000000) != 0)`.
  - If set, the file or directory represents **another named entity in the file system** (i.e., a path link/redirection such as a symbolic link, directory junction, or volume mount point).
  - If clear, the item is **not** an alias or link to another path; it represents data stored or virtualized by that specific file/directory itself (e.g. cloud files, deduplication, WOF compression).

### 4.2 Key Reparse Tag Values

| Reparse Tag Identifier | Hex Value | Name Surrogate Bit (`0x20000000`)? | Meaning |
| :--- | :--- | :--- | :--- |
| `IO_REPARSE_TAG_MOUNT_POINT` | `0xA0000003` | **Yes** (`1`) | Directory junction or volume mount point. Points to another directory/volume. |
| `IO_REPARSE_TAG_SYMLINK` | `0xA000000C` | **Yes** (`1`) | Symbolic link (file or directory). Points to another path. |
| `IO_REPARSE_TAG_GLOBAL_REPARSE` | `0xA0000019` | **Yes** (`1`) | Named pipe symbolic link. |
| `IO_REPARSE_TAG_APPEXECLINK` | `0x8000001B` | **No** (`0`) (Custom UWP execution alias; behaves like an app launch link) | Universal Windows Platform execution redirect. |
| `IO_REPARSE_TAG_CLOUD` | `0x9000001A` | **No** (`0`) | **Cloud Files filter (OneDrive, modern Dropbox).** Real file data virtualized in cloud. |
| `IO_REPARSE_TAG_CLOUD_1` .. `_F` | `0x9000101A` .. `0x9000F01A` | **No** (`0`) | Cloud Files filter range. |
| `IO_REPARSE_TAG_FILE_PLACEHOLDER`| `0x80000015` | **No** (`0`) | Legacy Windows 8.1 OneDrive placeholder. |
| `IO_REPARSE_TAG_DEDUP` | `0x80000013` | **No** (`0`) | Windows Server Data Deduplication filter. |
| `IO_REPARSE_TAG_WOF` | `0x80000017` | **No** (`0`) | Windows Overlay filter (system file compression). |

**Critical Distinction:**
`IO_REPARSE_TAG_CLOUD_*` has value `0x9000001A`.
- Bit 31 (`0x80000000` Microsoft bit) is 1.
- Bit 28 (`0x10000000` Directory/Latency bit) is 1.
- **Bit 29 (`0x20000000` Name Surrogate bit) is 0.**
Therefore, cloud files are **never** name surrogates. They do not point to another path or cause directory recursion cycles.

---

## 5. How .NET and Win32 Can Distinguish Safely

### 5.1 Pure .NET BCL APIs (`FileSystemInfo.LinkTarget`)
Starting in .NET 6 (.NET 6, 7, 8, 9, 10):
- **API:** `FileSystemInfo.LinkTarget` and `FileSystemInfo.ResolveLinkTarget(bool returnFinalTarget)`
  - **Primary Source:** Microsoft Learn, *FileSystemInfo.LinkTarget Property*  
    [https://learn.microsoft.com/en-us/dotnet/api/system.io.filesysteminfo.linktarget](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesysteminfo.linktarget)
  - **Primary Source:** dotnet/runtime Issue #82949 (*TarWriter treats File Deduplication reparse point flag as symbolic links*)  
    [https://github.com/dotnet/runtime/issues/82949](https://github.com/dotnet/runtime/issues/82949)
- **Internal Behavior in .NET Runtime:**
  The .NET runtime explicitly checks the reparse tag before populating `LinkTarget`. If the reparse tag is not a supported link type (such as `IO_REPARSE_TAG_SYMLINK` or `IO_REPARSE_TAG_MOUNT_POINT`), `FileSystemInfo.LinkTarget` returns **`null`**.
- **Safe Link Detection Pattern in C#:**
  ```csharp
  bool isNavigationLink = (fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                          && fileInfo.LinkTarget is not null;
  ```
  If `fileInfo.LinkTarget != null`, the item is a symlink or junction that targets an external path. If `LinkTarget == null`, the item is either not a link or is an internal filter reparse point (such as OneDrive/Dropbox cloud placeholder, deduplication chunk, or WOF compressed file).

### 5.2 Win32 Interop via `WIN32_FIND_DATA` or `FSCTL_GET_REPARSE_POINT`
If SnapSync enumerates files via P/Invoke or needs exact reparse tag inspection without initializing full `FileInfo` instances:
1. **From `WIN32_FIND_DATA`:**
   During directory enumeration with `FindFirstFileEx` / `FindNextFile`:
   - If `(dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0`, `dwReserved0` contains the 32-bit `ReparseTag`.
   - **Name surrogate test:**
     ```csharp
     bool isLinkOrJunction = (findData.dwReserved0 & 0x20000000) != 0;
     ```
   - **Cloud placeholder test:**
     ```csharp
     bool isCloudPlaceholder = (findData.dwReserved0 & 0x0000FFFF) == 0x001A; // IO_REPARSE_TAG_CLOUD family
     ```
2. **Preventing Accidental Hydration:**
   When opening files that might be in remote cloud storage, passing `FILE_FLAG_OPEN_NO_RECALL` (`0x00100000`) to `CreateFile` / `FileStream` will prevent `cldflt.sys` from recalling or downloading the file if it is offline. If the file is online-only, attempting to read recalled data will return `ERROR_CANT_ACCESS_FILE` (`0x80070780`) or fail cleanly without hanging or burning network bandwidth.

---

## 6. Concrete Recommendation for SnapSync

1. **Retire the Blanket `FileAttributes.ReparsePoint` Check:**
   SnapSync must **never** drop or skip files solely because `Attributes.HasFlag(FileAttributes.ReparsePoint)`. Doing so breaks sync trees located in OneDrive or Dropbox user directories.

2. **Differentiate Directory Traversal (Recursion Cycles) from File Copy:**
   - **For Directory Enumeration / Recursion:**
     SnapSync's goal in skipping reparse points during directory recursion is to avoid infinite loops and duplicate backups caused by directory symlinks and NTFS junctions (e.g. `C:\Users\User\Application Data -> C:\Users\User\AppData\Roaming`).
     - **Safe Rule for Directories:**
       Skip descending into a subdirectory only if:
       `dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint) && dirInfo.LinkTarget != null`
       (or `(tag & 0x20000000) != 0`).
       Cloud sync root folders and placeholder subdirectories have `LinkTarget == null` and must be traversed normally.
   - **For File Handling (Payload Copying):**
     - **Symbolic Links to Files:** If `fileInfo.LinkTarget != null`, treat as a symbolic link (either copy link metadata or ignore, depending on user settings).
     - **Cloud Files:** If `fileInfo.LinkTarget == null`, treat as a normal regular file.

3. **Handle Dehydrated / Online-Only Files Explicitly:**
   - If a file has `FileAttributes.ReparsePoint` and `fileInfo.LinkTarget == null`:
     - Check if it is unhydrated:
       `bool isDehydrated = (fileInfo.Attributes & FileAttributes.Offline) != 0 || ((int)fileInfo.Attributes & 0x00400000 /* RECALL_ON_DATA_ACCESS */) != 0;`
     - Provide clear sync engine policy:
       - *Policy A (Full Offline Backup / Hydrate):* Read the file normally. Windows will automatically hydrate it from OneDrive/Dropbox. (Caution: informs the user that network bandwidth will be used).
       - *Policy B (Local Only / No Network Recall):* If `isDehydrated` is true, skip reading the body or log a warning ("File is online-only in cloud storage and not hydrated locally"), rather than mistaking it for a corrupt symlink.

---

## 7. Sources and Citations

1. **Microsoft Learn:** *Build a Cloud Sync Engine that Supports Placeholder Files*  
   https://learn.microsoft.com/en-us/windows/win32/cfapi/build-a-cloud-file-sync-engine  
   *(Details `cldflt.sys`, placeholder states, hydration rules, and explicitly warns against confusing reparse points with symbolic links).*
2. **Microsoft Learn:** *[MS-FSCC]: File Attributes* (Section 2.6 File Attributes)  
   https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-fscc/ca28ec38-f155-4768-81d6-4bfeb8586fc9  
   *(Definitions of `FILE_ATTRIBUTE_REPARSE_POINT` 0x400, `FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS` 0x400000, and `FILE_ATTRIBUTE_OFFLINE` 0x1000).*
3. **Microsoft Learn:** *[MS-FSCC]: Reparse Tags* (Section 2.1.2.1 Reparse Tags)  
   https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-fscc/c8e77b37-3909-4fe6-a4ea-2b9d423b1ee4  
   *(Definitions of tag bit layout, Name Surrogate bit `0x20000000`, `IO_REPARSE_TAG_MOUNT_POINT` 0xA0000003, `IO_REPARSE_TAG_SYMLINK` 0xA000000C, and `IO_REPARSE_TAG_CLOUD` 0x9000001A).*
4. **Microsoft Learn:** *IsReparseTagNameSurrogate macro (ntifs.h)*  
   https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ntifs/nf-ntifs-isreparsetagnamesurrogate  
   *(Describes testing for name surrogates).*
5. **Microsoft Learn:** *RtlSetProcessPlaceholderCompatibilityMode function (ntifs.h)*  
   https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ntifs/nf-ntifs-rtlsetprocessplaceholdercompatibilitymode  
   *(Explains how Windows exposes or disguises placeholder reparse points to Win32 processes).*
6. **Dropbox Help:** *Expected changes with Dropbox for Windows update*  
   https://help.dropbox.com/installs/windows-support-for-expected-changes  
   *(First-party verification of Dropbox adopting the Windows Cloud Files API).*
7. **Syncthing Documentation:** *FAQ - What things are synchronized?*  
   https://docs.syncthing.net/users/faq.html  
   *(First-party documentation confirming Syncthing does not use placeholder reparse points and operates on standard local file system data).*
8. **.NET Runtime Repository:** *TarWriter on Windows Server treats File Deduplication reparse point flag as symbolic links (#82949)*  
   https://github.com/dotnet/runtime/issues/82949  
   *(First-party .NET runtime issue documenting that `FileSystemInfo.LinkTarget` returns `null` for non-link reparse points like dedup and cloud files, and confirming `FileAttributes.ReparsePoint` alone is insufficient to classify a link).*
