

/* this ALWAYS GENERATED file contains the definitions for the interfaces */


 /* File created by MIDL compiler version 8.00.0603 */
/* at Thu Mar 05 15:53:19 2015
 */
/* Compiler settings for BigStashExt.idl:
    Oicf, W1, Zp8, env=Win32 (32b run), target_arch=X86 8.00.0603 
    protocol : dce , ms_ext, c_ext, robust
    error checks: allocation ref bounds_check enum stub_data 
    VC __declspec() decoration level: 
         __declspec(uuid()), __declspec(selectany), __declspec(novtable)
         DECLSPEC_UUID(), MIDL_INTERFACE()
*/
/* @@MIDL_FILE_HEADING(  ) */

#pragma warning( disable: 4049 )  /* more than 64k source lines */


/* verify that the <rpcndr.h> version is high enough to compile this file*/
#ifndef __REQUIRED_RPCNDR_H_VERSION__
#define __REQUIRED_RPCNDR_H_VERSION__ 475
#endif

#include "rpc.h"
#include "rpcndr.h"

#ifndef __RPCNDR_H_VERSION__
#error this stub requires an updated version of <rpcndr.h>
#endif // __RPCNDR_H_VERSION__

#ifndef COM_NO_WINDOWS_H
#include "windows.h"
#include "ole2.h"
#endif /*COM_NO_WINDOWS_H*/

#ifndef __BigStashExt_i_h__
#define __BigStashExt_i_h__

#if defined(_MSC_VER) && (_MSC_VER >= 1020)
#pragma once
#endif

/* Forward Declarations */ 

#ifndef __IBigStashContextMenuExt_FWD_DEFINED__
#define __IBigStashContextMenuExt_FWD_DEFINED__
typedef interface IBigStashContextMenuExt IBigStashContextMenuExt;

#endif 	/* __IBigStashContextMenuExt_FWD_DEFINED__ */


#ifndef __BigStashContextMenuExt_FWD_DEFINED__
#define __BigStashContextMenuExt_FWD_DEFINED__

#ifdef __cplusplus
typedef class BigStashContextMenuExt BigStashContextMenuExt;
#else
typedef struct BigStashContextMenuExt BigStashContextMenuExt;
#endif /* __cplusplus */

#endif 	/* __BigStashContextMenuExt_FWD_DEFINED__ */


/* header files for imported files */
#include "oaidl.h"
#include "ocidl.h"

#ifdef __cplusplus
extern "C"{
#endif 


#ifndef __IBigStashContextMenuExt_INTERFACE_DEFINED__
#define __IBigStashContextMenuExt_INTERFACE_DEFINED__

/* interface IBigStashContextMenuExt */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IBigStashContextMenuExt;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("D51FA475-DE16-4D5A-B733-FB239F2F503C")
    IBigStashContextMenuExt : public IUnknown
    {
    public:
    };
    
    
#else 	/* C style interface */

    typedef struct IBigStashContextMenuExtVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            IBigStashContextMenuExt * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            IBigStashContextMenuExt * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            IBigStashContextMenuExt * This);
        
        END_INTERFACE
    } IBigStashContextMenuExtVtbl;

    interface IBigStashContextMenuExt
    {
        CONST_VTBL struct IBigStashContextMenuExtVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IBigStashContextMenuExt_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IBigStashContextMenuExt_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IBigStashContextMenuExt_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IBigStashContextMenuExt_INTERFACE_DEFINED__ */



#ifndef __BigStashExtLib_LIBRARY_DEFINED__
#define __BigStashExtLib_LIBRARY_DEFINED__

/* library BigStashExtLib */
/* [version][uuid] */ 


EXTERN_C const IID LIBID_BigStashExtLib;

EXTERN_C const CLSID CLSID_BigStashContextMenuExt;

#ifdef __cplusplus

class DECLSPEC_UUID("070FB75B-3783-4FA1-9065-F967737DD790")
BigStashContextMenuExt;
#endif
#endif /* __BigStashExtLib_LIBRARY_DEFINED__ */

/* Additional Prototypes for ALL interfaces */

/* end of Additional Prototypes */

#ifdef __cplusplus
}
#endif

#endif


