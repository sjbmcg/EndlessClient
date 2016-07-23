﻿// Original Work Copyright (c) Ethan Moffat 2014-2016
// This file is subject to the GPL v2 License
// For additional details, see the LICENSE file

using System.IO;

namespace EOLib.Localization
{
    public class DataFileLoadActions : IDataFileLoadActions
    {
        private readonly IDataFileRepository _dataFileRepository;

        public DataFileLoadActions(IDataFileRepository dataFileRepository)
        {
            _dataFileRepository = dataFileRepository;
        }

        public void LoadDataFiles()
        {
            var files = Directory.GetFiles(DataFileConstants.DataFilePath, "*.edf");

            if (!Directory.Exists(DataFileConstants.DataFilePath) ||
                files.Length != DataFileConstants.ExpectedNumberOfDataFiles)
                throw new DataFileLoadException();

            _dataFileRepository.DataFiles.Clear();
            for (int i = 1; i <= DataFileConstants.ExpectedNumberOfDataFiles; ++i)
            {
                if (!DataFileNameIsValid(i, files[i - 1]))
                    throw new DataFileLoadException();

                var fileToLoad = (DataFiles)i;
                var loadedFile = new EDFFile(files[i - 1], fileToLoad);

                _dataFileRepository.DataFiles.Add(fileToLoad, loadedFile);
            }
        }

        private bool DataFileNameIsValid(int fileNumber, string fileName)
        {
            var expectedFormat = string.Format("data/dat0{0:00}.edf", fileNumber);
            return expectedFormat == fileName;
        }
    }
}