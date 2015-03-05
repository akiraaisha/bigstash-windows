// BigStashContextMenuExt.cpp : Implementation of CBigStashContextMenuExt

#include "stdafx.h"
#include "BigStashContextMenuExt.h"

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