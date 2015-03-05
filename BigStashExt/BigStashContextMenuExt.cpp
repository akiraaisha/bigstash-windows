// BigStashContextMenuExt.cpp : Implementation of CBigStashContextMenuExt

#include "stdafx.h"
#include "BigStashContextMenuExt.h"
#include <strsafe.h>
#include <fstream>

#define IDM_STASH            0        // The command's identifier offset.  
#define VERB_STASHA        "Stash"    // The command's ANSI verb string 
#define VERB_STASHW        L"Stash"   // The command's Unicode verb string 

// CBigStashContextMenuExt

// 
//   FUNCTION: CFileContextMenuExt::Initialize(LPCITEMIDLIST, LPDATAOBJECT,  
//             HKEY) 
// 
//   PURPOSE: Initializes the context menu extension. 
// 
IFACEMETHODIMP CBigStashContextMenuExt::Initialize(
	LPCITEMIDLIST pidlFolder, LPDATAOBJECT pDataObj, HKEY hProgID)
{
	HRESULT hr = E_INVALIDARG;

	if (NULL == pDataObj)
	{
		return hr;
	}

	FORMATETC fe = { CF_HDROP, NULL, DVASPECT_CONTENT, -1, TYMED_HGLOBAL };
	STGMEDIUM stm;

	// pDataObj contains the objects being acted upon.
	// We want to get an HDROP handle for enumerating the selected files.
	if (SUCCEEDED(pDataObj->GetData(&fe, &stm)))
	{
		// Get an HDROP handle.
		HDROP hDrop = static_cast<HDROP>(GlobalLock(stm.hGlobal));
		if (hDrop != NULL)
		{
			// Determine how many files are involved in this operation.
			UINT nFiles = DragQueryFile(hDrop, 0xFFFFFFFF, NULL, 0);
			if (nFiles != 0)
			{
				// Enumerates the selected files and directories. 
				wchar_t szFileName[MAX_PATH];
				for (UINT i = 0; i < nFiles; i++)
				{
					// Get the next filename. 
					if (0 == DragQueryFile(hDrop, i, szFileName, MAX_PATH))
						continue;

					m_pathnames.push_back(szFileName);
				}

				hr = (m_pathnames.size() > 0) ? S_OK : E_INVALIDARG;
			}

			GlobalUnlock(stm.hGlobal);
		}

		ReleaseStgMedium(&stm);
	}

	// If any value other than S_OK is returned from the method, the context  
	// menu is not displayed. 
	return hr;
}


// 
//   FUNCTION: CFileContextMenuExt::OnStashClick(HWND) 
// 
//   PURPOSE: OnStashClick handles the "Stash" verb of the shell extension. 
// 
void CBigStashContextMenuExt::OnStashClick(HWND hWnd)
{
	if (!m_pathnames.empty())
	{
		std::wstring pathnames = m_pathnames[0];
		for (size_t i = 1; i < m_pathnames.size(); ++i)
		{
			pathnames += L"\n";
			pathnames += m_pathnames[i];
		}

		// clear the vector holding the paths.
		m_pathnames.clear();

		wchar_t* localAppDataPath = NULL;

		if (SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, NULL, &localAppDataPath) != S_OK)
		{
			MessageBox(hWnd, L"Error finding your Local AppData folder.", _T("BigStashExt"),
				MB_ICONERROR);
			return;
		}

		// Get a wstring from the localAppDataPath pointer.
		std::wstring selectionFilePath(localAppDataPath);

		// Free resource used for local app data path.
		CoTaskMemFree(static_cast<void*>(localAppDataPath));

		// Concat the BigStash folder path and the name of the file to save the paths.
		selectionFilePath += L"\\BigStash\\selectionfile.txt";

		// Get an ofstream to write to.
		std::ofstream selectionFile(selectionFilePath, std::ios::out | std::ios::binary);

		if (selectionFile.is_open())
		{
			selectionFile << to_utf8(pathnames);

			// close the file.
			selectionFile.close();
		}
		else
		{
			MessageBox(hWnd, L"Error preparing files for archiving.", _T("BigStashExt"),
				MB_ICONERROR);
			return;
		}

		STARTUPINFO si;
		PROCESS_INFORMATION pi;

		ZeroMemory(&si, sizeof(si));
		si.cb = sizeof(si);
		ZeroMemory(&pi, sizeof(pi));

		// Call BigStash app here.
	}
}

///////////////////////////////////////////////////////////////////////////// 
// Some helper methods
//

std::string to_utf8(const wchar_t* buffer, int len)
{
	int nChars = ::WideCharToMultiByte(
		CP_UTF8,
		0,
		buffer,
		len,
		NULL,
		0,
		NULL,
		NULL);
	if (nChars == 0) return "";

	std::string newbuffer;
	newbuffer.resize(nChars);
	::WideCharToMultiByte(
		CP_UTF8,
		0,
		buffer,
		len,
		const_cast< char* >(newbuffer.c_str()),
		nChars,
		NULL,
		NULL);

	return newbuffer;
}

std::string to_utf8(const std::wstring& str)
{
	return to_utf8(str.c_str(), (int)str.size());
}