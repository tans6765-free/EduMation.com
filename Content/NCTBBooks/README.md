# NCTB Book Storage

Keep official NCTB PDFs outside `wwwroot` so they are not publicly downloadable by URL.
Do not commit the PDFs to GitHub. Store them locally or in private object storage.

Use this structure:

```text
Content/
  NCTBBooks/
    Class-01/
      Bangla/
      English/
    Class-02/
      Bangla/
      English/
    Class-03/
      Bangla/
      English/
    Class-04/
      Bangla/
      English/
    Class-05/
      Bangla/
      English/
    Class-06/
      Bangla/
      English/
    Class-07/
      Bangla/
      English/
    Class-08/
      Bangla/
      English/
    Class-09-10/
      Bangla/
      English/
    Class-11-12/
      Bangla/
      English/
```

Use the official book title in the filename, for example:

```text
Content/NCTBBooks/Class-05/Bangla/Mathematics.pdf
Content/NCTBBooks/Class-05/English/Mathematics.pdf
```

The importer should record class range, language, subject, academic year, source filename, checksum, and import status. The PDF is the source document; extracted chapters, topics, lesson context, and Gemini-generated questions belong in the database.
