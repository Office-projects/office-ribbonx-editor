﻿using System.IO;

using NUnit.Framework;

namespace OfficeRibbonXEditor.Models
{
    [TestFixture]
    public class OfficeDocumentTests
    {
        private readonly string sourceFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "Resources/Blank.xlsx");

        private readonly string destFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "Output/BlankSaved.xlsx");

        [SetUp]
        public void SetUp()
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            Directory.CreateDirectory(Path.GetDirectoryName(this.destFile));

            if (File.Exists(this.destFile))
            {
                File.Delete(this.destFile);
            }
        }

        [Test]
        public void DocumentShouldBeOpened()
        {
            // Arrange / act
            var doc = new OfficeDocument(this.sourceFile);

            // Assert
            Assert.IsNotNull(doc.UnderlyingPackage, "Package was not opened");
        }

        [Test]
        public void PartShouldBeCreated()
        {
            // Arrange
            var doc = new OfficeDocument(this.sourceFile);

            // Act
            var part = doc.CreateCustomPart(XmlParts.RibbonX12);

            // Assert
            Assert.IsNotNull(part, "Part was not inserted");
        }

        [Test]
        public void DocumentShouldBeSaved()
        {
            // Arrange
            var doc = new OfficeDocument(this.sourceFile);

            // Act
            doc.Save(this.destFile);

            // Assert
            Assert.IsTrue(File.Exists(this.destFile), "File was not saved");
        }
    }
}
