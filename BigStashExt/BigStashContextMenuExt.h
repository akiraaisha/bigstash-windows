// BigStashContextMenuExt.h : Declaration of the CBigStashContextMenuExt

#pragma once
#include "resource.h"       // main symbols



#include "BigStashExt_i.h"



#if defined(_WIN32_WCE) && !defined(_CE_DCOM) && !defined(_CE_ALLOW_SINGLE_THREADED_OBJECTS_IN_MTA)
#error "Single-threaded COM objects are not properly supported on Windows CE platform, such as the Windows Mobile platforms that do not include full DCOM support. Define _CE_ALLOW_SINGLE_THREADED_OBJECTS_IN_MTA to force ATL to support creating single-thread COM object's and allow use of it's single-threaded COM object implementations. The threading model in your rgs file was set to 'Free' as that is the only threading model supported in non DCOM Windows CE platforms."
#endif

using namespace ATL;


// CBigStashContextMenuExt

class ATL_NO_VTABLE CBigStashContextMenuExt :
	public CComObjectRootEx<CComSingleThreadModel>,
	public CComCoClass<CBigStashContextMenuExt, &CLSID_BigStashContextMenuExt>,
	public IShellExtInit,
	public IContextMenu
{
public:
	CBigStashContextMenuExt()
	{
	}

DECLARE_REGISTRY_RESOURCEID(IDR_BIGSTASHCONTEXTMENUEXT)

DECLARE_NOT_AGGREGATABLE(CBigStashContextMenuExt)

BEGIN_COM_MAP(CBigStashContextMenuExt)
	COM_INTERFACE_ENTRY(IShellExtInit)
	COM_INTERFACE_ENTRY(IContextMenu)
END_COM_MAP()



	DECLARE_PROTECT_FINAL_CONSTRUCT()

	HRESULT FinalConstruct()
	{
		return S_OK;
	}

	void FinalRelease()
	{
	}

public:

	// IShellExtInit
	IFACEMETHODIMP Initialize(LPCITEMIDLIST, LPDATAOBJECT, HKEY);

	// IContextMenu
	IFACEMETHODIMP GetCommandString(UINT_PTR, UINT, UINT*, LPSTR, UINT);
	IFACEMETHODIMP InvokeCommand(LPCMINVOKECOMMANDINFO);
	IFACEMETHODIMP QueryContextMenu(HMENU, UINT, UINT, UINT, UINT);


};

OBJECT_ENTRY_AUTO(__uuidof(BigStashContextMenuExt), CBigStashContextMenuExt)
