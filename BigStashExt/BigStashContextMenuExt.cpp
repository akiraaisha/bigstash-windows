// BigStashContextMenuExt.cpp : Implementation of CBigStashContextMenuExt

#include "stdafx.h"
#include "BigStashContextMenuExt.h"
#include <strsafe.h>
#include <fstream>

#define IDM_STASH            0        // The command's identifier offset.  
#define VERB_STASHA        "Stash"    // The command's ANSI verb string 
#define VERB_STASHW        L"Stash"   // The command's Unicode verb string 
#define KEY_LATEST_VERSION_PATH L"LatestVersionPath"

///////////////////////////////////////////////////////////////////////////// 
// CBigStashContextMenuExt IShellExtInit methods. 
//  

// 
//   FUNCTION: CBigStashContextMenuExt::Initialize(LPCITEMIDLIST, LPDATAOBJECT,  
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


///////////////////////////////////////////////////////////////////////////// 
// CBigStashContextMenuExt IContextMenu methods. 
//  

// 
//   FUNCTION: CBigStashContextMenuExt::QueryContextMenu(HMENU, UINT, UINT, UINT,  
//             UINT) 
// 
//   PURPOSE: The Shell calls IContextMenu::QueryContextMenu to allow the  
//            context menu handler to add its menu items to the menu. It  
//            passes in the HMENU handle in the hmenu parameter. The  
//            indexMenu parameter is set to the index to be used for the  
//            first menu item that is to be added. 
// 
IFACEMETHODIMP CBigStashContextMenuExt::QueryContextMenu(
	HMENU hMenu, UINT indexMenu, UINT idCmdFirst, UINT idCmdLast, UINT uFlags)
{
	// If uFlags include CMF_DEFAULTONLY then we should not do anything.
	if (CMF_DEFAULTONLY & uFlags)
	{
		return MAKE_HRESULT(SEVERITY_SUCCESS, 0, USHORT(0));
	}

	// If uFlags include CMF_VERBSONLY then we should not do anything. (shortcut target)
	if (CMF_VERBSONLY  & uFlags)
	{
		return MAKE_HRESULT(SEVERITY_SUCCESS, 0, USHORT(0));
	}

	// Use either InsertMenu or InsertMenuItem to add menu items to the list.
	InsertMenu(hMenu, indexMenu, MF_STRING | MF_BYPOSITION, idCmdFirst +
		IDM_STASH, _T("&Stash away"));

	// Set the bitmap for the register item.
	if (NULL != m_hRegBmp)
		SetMenuItemBitmaps(hMenu, indexMenu, MF_BITMAP | MF_BYPOSITION, m_hRegBmp, NULL);

	// Return an HRESULT value with the severity set to SEVERITY_SUCCESS.
	// Set the code value to the offset of the largest command identifier
	// that was assigned, plus one (1).
	return MAKE_HRESULT(SEVERITY_SUCCESS, 0, USHORT(IDM_STASH + 1));
}


// 
//   FUNCTION: CBigStashContextMenuExt::GetCommandString(UINT, UINT, LPUINT,  
//             LPSTR, UINT) 
// 
//   PURPOSE: If a user highlights one of the items added by a context menu  
//            handler, the handler's IContextMenu::GetCommandString method is  
//            called to request a Help text string that will be displayed on  
//            the Windows Explorer status bar. This method can also be called  
//            to request the verb string that is assigned to a command.  
//            Either ANSI or Unicode verb strings can be requested. 
// 
IFACEMETHODIMP CBigStashContextMenuExt::GetCommandString(
	UINT_PTR idCommand, UINT uFlags, LPUINT lpReserved, LPSTR pszName,
	UINT uMaxNameLen)
{
	HRESULT hr = E_INVALIDARG;

	// For the command "&Shell Extension Test" (IDM_STASH)
	if (idCommand == IDM_STASH)
	{
		switch (uFlags)
		{
		case GCS_HELPTEXTA:
			hr = StringCchCopyNA(pszName,
				lstrlenA(pszName) / sizeof(pszName[0]),
				"Stash away with BigStash",
				uMaxNameLen);
			break;

		case GCS_HELPTEXTW:
			hr = StringCchCopyNW((LPWSTR)pszName,
				lstrlenW((LPWSTR)pszName) / sizeof(pszName[0]),
				L"Stash away with BigStash",
				uMaxNameLen);
			break;

		case GCS_VERBA:
			hr = StringCchCopyNA(pszName,
				lstrlenA(pszName) / sizeof(pszName[0]),
				VERB_STASHA, uMaxNameLen);
			break;

		case GCS_VERBW:
			hr = StringCchCopyNW((LPWSTR)pszName,
				lstrlenW((LPWSTR)pszName) / sizeof(pszName[0]),
				VERB_STASHW, uMaxNameLen);
			break;

		default:
			hr = S_OK;
		}
	}

	// If the command (idCommand) is not supported by this context menu
	// extension handler, returns E_INVALIDARG.

	return hr;
}


// 
//   FUNCTION: CBigStashContextMenuExt::InvokeCommand(LPCMINVOKECOMMANDINFO) 
// 
//   PURPOSE: This method is called when a user clicks a menu item to tell  
//            the handler to run the associated command. The lpcmi parameter  
//            points to a structure that contains the needed information. 
// 
IFACEMETHODIMP CBigStashContextMenuExt::InvokeCommand(LPCMINVOKECOMMANDINFO lpcmi)
{
	BOOL fEx = FALSE;
	BOOL fUnicode = FALSE;

	// Determines which structure is being passed in, CMINVOKECOMMANDINFO or  
	// CMINVOKECOMMANDINFOEX based on the cbSize member of lpcmi. Although  
	// the lpcmi parameter is declared in Shlobj.h as a CMINVOKECOMMANDINFO  
	// structure, in practice it often points to a CMINVOKECOMMANDINFOEX  
	// structure. This struct is an extended version of CMINVOKECOMMANDINFO  
	// and has additional members that allow Unicode strings to be passed. 
	if (lpcmi->cbSize == sizeof(CMINVOKECOMMANDINFOEX))
	{
		fEx = TRUE;
		if ((lpcmi->fMask & CMIC_MASK_UNICODE))
		{
			fUnicode = TRUE;
		}
	}

	// Determines whether the command is identified by its offset or verb. 
	// There are two ways to identify commands: 
	//   1) The command's verb string  
	//   2) The command's identifier offset 
	// If the high-order word of lpcmi->lpVerb (for the ANSI case) or  
	// lpcmi->lpVerbW (for the Unicode case) is nonzero, lpVerb or lpVerbW  
	// holds a verb string. If the high-order word is zero, the command  
	// offset is in the low-order word of lpcmi->lpVerb. 

	// For the ANSI case, if the high-order word is not zero, the command's
	// verb string is in lpcmi->lpVerb.
	if (!fUnicode && HIWORD(lpcmi->lpVerb))
	{
		// Is the verb supported by this context menu extension?
		if (StrCmpIA(lpcmi->lpVerb, VERB_STASHA) == 0)
		{
			OnStashClick(lpcmi->hwnd);
		}
		else
		{
			// If the verb is not recognized by the context menu handler, it
			// must return E_FAIL to allow it to be passed on to the other
			// context menu handlers that might implement that verb.
			return E_FAIL;
		}
	}

	// For the Unicode case, if the high-order word is not zero, the
	// command's verb string is in lpcmi->lpVerbW.
	else if (fUnicode && HIWORD(((CMINVOKECOMMANDINFOEX*)lpcmi)->lpVerbW))
	{
		// Is the verb supported by this context menu extension?
		if (StrCmpIW(((CMINVOKECOMMANDINFOEX*)lpcmi)->lpVerbW,
			VERB_STASHW) == 0)
		{
			OnStashClick(lpcmi->hwnd);
		}
		else
		{
			// If the verb is not recognized by the context menu handler, it
			// must return E_FAIL to allow it to be passed to the other
			// context menu handlers that might implement that verb.
			return E_FAIL;
		}
	}

	// If the command cannot be identified through the verb string, then  
	// check the identifier offset. 
	else
	{
		// Is the command identifier offset supported by this context menu  
		// extension? 
		if (LOWORD(lpcmi->lpVerb) == IDM_STASH)
		{
			OnStashClick(lpcmi->hwnd);
		}
		else
		{
			// If the verb is not recognized by the context menu handler, it  
			// must return E_FAIL to allow it to be passed on to the other  
			// context menu handlers that might implement that verb. 
			return E_FAIL;
		}
	}

	return S_OK;
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


///////////////////////////////////////////////////////////////////////////// 
// CBigStashContextMenuExt methods 
// (exluding the ones from implemented interfaces.
//

// 
//   FUNCTION: CBigStashContextMenuExt::OnStashClick(HWND) 
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

		// Let's find out the path of the executable we want to run.
		CRegKey reg;
		LONG    lRet;

		lRet = reg.Open(HKEY_CURRENT_USER,
			_T("Software\\BigStash\\BigStashWindows"),
			KEY_READ);

		if (ERROR_SUCCESS != lRet)
		{
			MessageBox(hWnd, L"Could not locate the BigStash application.\n\nError: Registry access denied.", 
				_T("BigStashExt"), MB_ICONERROR);
			return;
		}

		// The size of the value should fit in a buffer of size MAX_PATH = 260
		// since this is the max allowed path length in Windows.
		TCHAR valueName[MAX_PATH];
		ULONG length = MAX_PATH;

		lRet = reg.QueryStringValue(KEY_LATEST_VERSION_PATH, valueName, &length);

		if (ERROR_SUCCESS != lRet)
		{
			MessageBox(hWnd, L"Could not locate the BigStash application.\n\nError: Registry string value not valid.", _T("BigStashExt"),
				MB_ICONERROR);
			return;
		}
		

		// Call BigStash app here.
		ExecuteProcess(valueName, L"-u --fromfile \"" + selectionFilePath + L"\"", 1);
	}
}


//
//   FUNCTION CBigStashContextMenuExt::ExecuteProcess(std::wstring, std::wstring, size_t)
//
//   PURPOSE: ExecuteProcess executes the executable file located at the first wstring parameter,
//            with parameters passed as the second wstring parameter.
//			  size_t parameter indicates the time to wait on the called process until it's signaled.
//
size_t CBigStashContextMenuExt::ExecuteProcess(std::wstring fullPathToExe, std::wstring parameters, size_t secondsToWait)
{
	size_t iMyCounter = 0, iReturnVal = 0, iPos = 0;
	DWORD dwExitCode = 0;
	std::wstring sTempStr = L"";

	/* - NOTE - You should check here to see if the exe even exists */

	/* Add a space to the beginning of the Parameters */
	if (parameters.size() != 0)
	{
		if (parameters[0] != L' ')
		{
			parameters.insert(0, L" ");
		}
	}

	/* The first parameter needs to be the exe itself */
	sTempStr = fullPathToExe;
	iPos = sTempStr.find_last_of(L"\\");
	sTempStr.erase(0, iPos + 1);
	parameters = sTempStr.append(parameters);

	/* CreateProcessW can modify Parameters thus we allocate needed memory */
	wchar_t * pwszParam = new wchar_t[parameters.size() + 1];
	if (pwszParam == 0)
	{
		return 1;
	}
	const wchar_t* pchrTemp = parameters.c_str();
	wcscpy_s(pwszParam, parameters.size() + 1, pchrTemp);

	/* CreateProcess API initialization */
	STARTUPINFOW siStartupInfo;
	PROCESS_INFORMATION piProcessInfo;
	memset(&siStartupInfo, 0, sizeof(siStartupInfo));
	memset(&piProcessInfo, 0, sizeof(piProcessInfo));
	siStartupInfo.cb = sizeof(siStartupInfo);

	if (CreateProcessW(const_cast<LPCWSTR>(fullPathToExe.c_str()),
		pwszParam, 0, 0, false,
		CREATE_DEFAULT_ERROR_MODE, 0, 0,
		&siStartupInfo, &piProcessInfo) != false)
	{
		/* Watch the process. */
		dwExitCode = WaitForSingleObject(piProcessInfo.hProcess, (secondsToWait * 1000));
	}
	else
	{
		/* CreateProcess failed */
		iReturnVal = GetLastError();
	}

	/* Free memory */
	delete[]pwszParam;
	pwszParam = 0;

	/* Release handles */
	CloseHandle(piProcessInfo.hProcess);
	CloseHandle(piProcessInfo.hThread);

	return iReturnVal;
}
