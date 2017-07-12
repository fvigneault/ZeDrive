﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShareLibrary.Models;
using ShareLibrary.Summary;
using Action = ShareLibrary.Models.Action;

namespace ShareLibraryTests
{
    [TestClass]
    public class GroupSummaryTests
    {
        private const string TESTING_PATH = "./";
        private const string GROUP1 = "GROUP1";
        private const string GROUP2 = "GROUP2";
        private const int NUMBER_OF_TEST_FILE = 5;
        private static readonly IList<string> GROUPS = new ReadOnlyCollection<string>( new List<string> { GROUP1, GROUP2 });
        private List<string> TestFilesGroup1;

        [TestInitialize()]
        public void Initialize()
        {
            foreach (string group in GROUPS)
            {
                Directory.CreateDirectory(Path.Combine(TESTING_PATH, group));
                IEnumerable<string> filePaths = Directory.EnumerateFiles(Path.Combine(TESTING_PATH, group));
                foreach (string filePath in filePaths)
                {
                    File.Delete(Path.Combine(filePath));
                }

            }
            TestFilesGroup1 = new List<string>();
            for (int i = 0; i < NUMBER_OF_TEST_FILE; ++i)
            {
                string file = Path.Combine(TESTING_PATH, GROUP1, Path.GetRandomFileName());
                TestFilesGroup1.Add(file);
                File.Create(file).Close();
            }
        }

        [TestCleanup()]
        public void Cleanup()
        {
            TestFilesGroup1.ForEach(File.Delete);
            foreach (string group in GROUPS)
            {
                IEnumerable<string> filePaths = Directory.EnumerateFiles(Path.Combine(TESTING_PATH, group));
                foreach (string filePath in filePaths)
                {
                    File.Delete(filePath);
                }
                Directory.Delete(Path.Combine(TESTING_PATH, group));
            }
        }

        [TestMethod]
        public void ConstructorTest()
        {
            List<string> files = new List<string>{ Path.Combine(TESTING_PATH, GROUP2, "file1.jp"), Path.Combine(TESTING_PATH, GROUP2, "file2.jp"), Path.Combine(TESTING_PATH, GROUP2, "file3.jp") };

            foreach (string file in files)
            {
                File.Create(file).Close();
            }
            List<DateTime> filesCreations = new List<DateTime>{ new DateTime(2021, 9, 12, 17, 12, 4), new DateTime(2000, 1, 1, 0, 0, 0), new DateTime(2017, 7, 12, 12, 15, 4) };
            List<DateTime> filesModifications = new List<DateTime> { new DateTime(2021, 9, 12, 17, 12, 4), new DateTime(2001, 1, 1, 0, 0, 0), new DateTime(2017, 7, 12, 12, 15, 5) };

            for (int i = 0; i < files.Count; i++)
            {
                File.SetCreationTime(files[i], filesCreations[i]);
                File.SetLastWriteTime(files[i], filesModifications[i]);
            }

            GroupSummary group2Summary = new GroupSummary(GROUP2, TESTING_PATH);
            
            Assert.AreEqual(GROUP2, group2Summary.GroupName);
            Assert.AreEqual(3, group2Summary.Files.Count);
            for (int i = 0; i < group2Summary.Files.Count; i++)
            {
                Assert.AreEqual($"file{i+1}.jp", group2Summary.Files[i].Name);
                Assert.AreEqual(filesCreations[i], group2Summary.Files[i].CreationDate);
                Assert.AreEqual(filesModifications[i], group2Summary.Files[i].LastModificationDate);
            }

            files.ForEach(File.Delete);
        }

        [TestMethod]
        public void UpdateValidation()
        {
            //tester que les fichiers sont bien ajouter
            GroupSummary groupSummary = new GroupSummary(GROUP1, TESTING_PATH);
            Assert.AreEqual(NUMBER_OF_TEST_FILE, groupSummary.Files.Count);
            string fileAdded = Path.Combine(TESTING_PATH, GROUP1, Path.GetRandomFileName());
            File.Create(fileAdded).Close();
            groupSummary.Update();
            Assert.AreEqual(NUMBER_OF_TEST_FILE+1, groupSummary.Files.Count);
            Assert.IsTrue(groupSummary.Files.Exists(info => info.Name == Path.GetFileName(fileAdded)));

            File.Delete(fileAdded);
        }
        
        [TestMethod]
        public void EqualValidation()
        {
            string fileAdded = Path.Combine(TESTING_PATH, GROUP1, Path.GetRandomFileName());

            GroupSummary sum1g1 = new GroupSummary(GROUP1, TESTING_PATH);
            //When we create a file, a stream is created, so we need to close the stream because we are done with it.
            File.Create(fileAdded).Close();
            GroupSummary sum2g1 = new GroupSummary(GROUP1, TESTING_PATH);
            GroupSummary sum1g2 = new GroupSummary(GROUP2, TESTING_PATH);

            Assert.IsTrue(sum1g1 == sum1g1);
            Assert.IsFalse(sum1g1 != sum1g1);
            Assert.IsTrue(sum1g1 == sum2g1);
            Assert.IsFalse(sum1g1 != sum2g1);

            Assert.IsTrue(sum1g1 != sum1g2);
            Assert.IsFalse(sum1g1 == sum1g2);

            Assert.IsFalse(sum1g1 == null);
            Assert.IsTrue(sum1g1 != null);
            File.Delete(fileAdded);
        }

        [TestMethod]
        public void GenerateRevision()
        {
            string fileAdded = Path.Combine(TESTING_PATH, GROUP1, Path.GetRandomFileName());
            string fileDeleted = Path.Combine(TESTING_PATH, GROUP1, Path.GetRandomFileName());
            string fileModifed = Path.Combine(TESTING_PATH, GROUP1, Path.GetRandomFileName());
            File.Create(fileDeleted).Close();
            File.Create(fileModifed).Close();

            GroupSummary oldSummary = new GroupSummary(GROUP1, TESTING_PATH);
            //When we create a file, a stream is created, so we need to close the stream because we are done with it.
            File.Create(fileAdded).Close();
            File.Delete(fileDeleted);
            File.SetLastWriteTime(fileModifed, DateTime.Now);
            GroupSummary newSummary = new GroupSummary(GROUP1, TESTING_PATH);
            List<Revision> result = newSummary.GenerateRevisions(oldSummary);

            Assert.AreEqual(3, result.Count);
            //We are suppose to have only one new file.
            Assert.AreEqual(1, result.Count(revision => revision.Action == Action.Create));
            Revision createRevision = result.Find(revision => revision.Action == Action.Create);
            Assert.AreEqual(Path.GetFileName(fileAdded), createRevision.File.Name);
            Assert.AreEqual(GROUP1, createRevision.GroupName);

            Assert.AreEqual(1, result.Count(revision => revision.Action == Action.Delete));
            Revision deleteRevision = result.Find(revision => revision.Action == Action.Delete);
            Assert.AreEqual(Path.GetFileName(fileDeleted), deleteRevision.File.Name);
            Assert.AreEqual(GROUP1, deleteRevision.GroupName);

            Assert.AreEqual(1, result.Count(revision => revision.Action == Action.Modify));
            Revision modifyRevision = result.Find(revision => revision.Action == Action.Modify);
            Assert.AreEqual(Path.GetFileName(fileModifed), modifyRevision.File.Name);
            Assert.AreEqual(GROUP1, modifyRevision.GroupName);

            File.Delete(fileAdded);
            File.Delete(fileModifed);
        }

        //TODO test le contenu des revision(genre le data des fichiers)
        //public void 
    }
}
