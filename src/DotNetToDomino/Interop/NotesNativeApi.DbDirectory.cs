using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
namespace DotNetToDomino.Interop;
internal sealed partial class NotesNativeApi
{
 internal string[] ListDatabases(string server,int type)
 {
  EnsureInitialized(); using var thread=EnterThread(); const ushort SearchFileType=4,SearchSummary=2,FileRecurse=8192;
  ushort fileType=type switch{NotesConst.REPLICA_CANDIDATE=>1,NotesConst.TEMPLATE_CANDIDATE=>2,NotesConst.DATABASE=>4,NotesConst.TEMPLATE=>5,_=>throw new NotesException(5,"Invalid NotesDBDirectory database type.")};
  var directory=OpenDatabase(server,string.Empty);var paths=new List<string>();Exception? callbackError=null;
  DbDirectorySearchCallback callback=(_,_,summary)=>{if(callbackError is not null)return 0;try{if(summary!=0&&TryGetDirectorySummaryText(summary,"$Path",out var path)&&path.Length!=0){var relative=path.Replace('\\','/').Trim();if(relative.StartsWith('/')||(relative.Length>=2&&char.IsLetter(relative[0])&&relative[1]==':')||relative.Split('/',StringSplitOptions.RemoveEmptyEntries).Any(segment=>segment==".."))throw new NotesException(5,"NotesDBDirectory returned an invalid relative database path: "+relative);if(relative.Length!=0)paths.Add(relative);}}catch(Exception ex){callbackError=ex;}return 0;};
  try{CheckStatus(Resolve<DbDirectoryNSFSearch>("NSFSearch")(directory,0,0,SearchFileType|SearchSummary,(ushort)(fileType|FileRecurse),0,callback,0,0),"NSFSearch(database directory)");GC.KeepAlive(callback);if(callbackError is not null)ExceptionDispatchInfo.Capture(callbackError).Throw();}finally{CloseDatabase(directory);}
  return paths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path=>path,StringComparer.OrdinalIgnoreCase).ToArray();
 }
 private bool TryGetDirectorySummaryText(nint summary,string itemName,out string value){const int capacity=ushort.MaxValue;using var name=ToLmbcs(itemName);var buffer=Marshal.AllocHGlobal(capacity);try{Zero(buffer,capacity);var found=Resolve<DbDirectoryNSFGetSummaryValue>("NSFGetSummaryValue")(summary,name.Pointer,buffer,ushort.MaxValue);value=found==0?string.Empty:FromLmbcsZeroTerminated(buffer,capacity);return found!=0;}finally{Marshal.FreeHGlobal(buffer);}}
 [UnmanagedFunctionPointer(CallingConvention.Winapi)]private delegate ushort DbDirectoryNSFSearch(nint db,nint formula,nint viewTitle,ushort searchFlags,ushort noteClassMask,nint since,DbDirectorySearchCallback callback,nint parameter,nint until);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)]private delegate ushort DbDirectorySearchCallback(nint parameter,nint searchMatch,nint summaryBuffer);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)]private delegate int DbDirectoryNSFGetSummaryValue(nint summaryBuffer,nint itemName,nint itemValue,ushort maximumLength);
}
