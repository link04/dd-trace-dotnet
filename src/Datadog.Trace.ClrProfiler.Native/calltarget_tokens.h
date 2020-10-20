#ifndef DD_CLR_PROFILER_CALLTARGET_TOKENS_H_
#define DD_CLR_PROFILER_CALLTARGET_TOKENS_H_

#include <corhlpr.h>

#include <unordered_map>
#include <unordered_set>

#include "clr_helpers.h"
#include "com_ptr.h"
#include "integration.h"
#include "string.h"

namespace trace {

class CallTargetTokens {
 private:
  void* module_metadata_ptr = nullptr;

  // CorLib tokens
  mdAssemblyRef corLibAssemblyRef = mdAssemblyRefNil;
  mdTypeRef objectTypeRef = mdTypeRefNil;
  mdTypeRef exTypeRef = mdTypeRefNil;
  mdTypeRef typeRef = mdTypeRefNil;
  mdTypeRef runtimeTypeHandleRef = mdTypeRefNil;
  mdToken getTypeFromHandleToken = mdTokenNil;
  mdTypeRef runtimeMethodHandleRef = mdTypeRefNil;

  // CallTarget tokens
  mdAssemblyRef profilerAssemblyRef = mdAssemblyRefNil;
  mdTypeRef callTargetTypeRef = mdTypeRefNil;
  mdTypeRef callTargetStateTypeRef = mdTypeRefNil;
  mdTypeRef callTargetReturnVoidTypeRef = mdTypeRefNil;
  mdTypeRef callTargetReturnTypeRef = mdTypeRefNil;

  mdMemberRef beginArrayMemberRef = mdMemberRefNil;
  mdMemberRef beginP0MemberRef = mdMemberRefNil;
  mdMemberRef beginP1MemberRef = mdMemberRefNil;
  mdMemberRef beginP2MemberRef = mdMemberRefNil;
  mdMemberRef beginP3MemberRef = mdMemberRefNil;
  mdMemberRef beginP4MemberRef = mdMemberRefNil;
  mdMemberRef beginP5MemberRef = mdMemberRefNil;
  mdMemberRef beginP6MemberRef = mdMemberRefNil;

  mdMemberRef endReturnMemberRef = mdMemberRefNil;
  mdMemberRef endVoidMemberRef = mdMemberRefNil;

  mdMemberRef logExceptionRef = mdMemberRefNil;
  mdMemberRef getDefaultMemberRef = mdMemberRefNil;

  inline ModuleMetadata* GetMetadata() {
    return (ModuleMetadata*)module_metadata_ptr;
  }
  HRESULT EnsureCorLibTokens();
  HRESULT EnsureBaseCalltargetTokens();

 public:
  CallTargetTokens(void* module_metadata_ptr) {
    this->module_metadata_ptr = module_metadata_ptr;
  }

  mdMethodSpec GetBeginMethodWithArgumentsArrayMemberRef(
      mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef);
  mdMethodSpec GetBeginMethodWithoutArgumentsMemberRef(
      mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef);
  mdMethodSpec GetBeginMethodWith1ArgumentsMemberRef(
      mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef,
      mdTypeRef arg1TypeRef);
  mdMethodSpec GetBeginMethodWith2ArgumentsMemberRef(
      mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef,
      mdTypeRef arg1TypeRef, mdTypeRef arg2TypeRef);
  mdMethodSpec GetBeginMethodWith3ArgumentsMemberRef(
      mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef,
      mdTypeRef arg1TypeRef, mdTypeRef arg2TypeRef, mdTypeRef arg3TypeRef);
  mdMethodSpec GetBeginMethodWith4ArgumentsMemberRef(
      mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef,
      mdTypeRef arg1TypeRef, mdTypeRef arg2TypeRef, mdTypeRef arg3TypeRef,
      mdTypeRef arg4TypeRef);
  mdMethodSpec GetBeginMethodWith5ArgumentsMemberRef(
      mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef,
      mdTypeRef arg1TypeRef, mdTypeRef arg2TypeRef, mdTypeRef arg3TypeRef,
      mdTypeRef arg4TypeRef, mdTypeRef arg5TypeRef);
  mdMethodSpec GetBeginMethodWith6ArgumentsMemberRef(
      mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef,
      mdTypeRef arg1TypeRef, mdTypeRef arg2TypeRef, mdTypeRef arg3TypeRef,
      mdTypeRef arg4TypeRef, mdTypeRef arg5TypeRef, mdTypeRef arg6TypeRef);

  mdMethodSpec GetEndVoidReturnMemberRef(mdTypeRef integrationTypeRef,
                                         mdTypeRef currentTypeRef);
  mdMethodSpec GetEndReturnMemberRef(mdTypeRef integrationTypeRef,
                                     mdTypeRef currentTypeRef,
                                     mdTypeRef returnTypeRef);

  mdMethodSpec GetLogExceptionMemberRef(mdTypeRef integrationTypeRef,
                                        mdTypeRef currentTypeRef);

  mdTypeRef GetTargetStateTypeRef();
  mdTypeRef GetTargetVoidReturnTypeRef();
  mdTypeSpec GetTargetReturnValueTypeRef(mdTypeRef returnTypeRef);
};

}  // namespace trace

#endif  // DD_CLR_PROFILER_CALLTARGET_TOKENS_H_