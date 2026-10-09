# NCTB source books

The default source folder is this folder. If drive B does not have enough space, set `Nctb:RootPath` to a folder on another drive instead, such as `D:\EduMation-NCTB`.

Paste the supplied books under the configured folder using this structure:

```text
Content/NCTB/
  Class 1/
    Bangla Version/
      *.pdf
    English Version/
      *.pdf
  Class 2/
    Bangla Version/
    English Version/
  Class 3/
    Bangla Version/
    English Version/
  Class 4/
    Bangla Version/
    English Version/
  Class 5/
    Bangla Version/
    English Version/
  Class 6/
    Bangla Version/
    English Version/
  Class 7/
    Bangla Version/
    English Version/
  Class 8/
    Bangla Version/
    English Version/
  Class 9-10/
    Bangla Version/
    English Version/
  Class 11-12/
    Bangla Version/
    English Version/
```

Keep the original PDF filenames. The importer will record the class, language, source filename, academic year, verification status, and extracted page metadata.

PDFs are intentionally ignored by Git and will not be pushed to GitHub. Do not put API keys or student data in this folder.
