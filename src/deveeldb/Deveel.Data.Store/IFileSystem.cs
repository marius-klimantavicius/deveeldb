﻿using System;

namespace Deveel.Data.Store {
	public interface IFileSystem {
		bool FileExists(string path);

		IFile OpenFile(string path, bool readOnly);

		IFile CreateFile(string path);

		bool DeleteFile(string path);
	}
}
