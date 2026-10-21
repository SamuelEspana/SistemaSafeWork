using System;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Web;
using System.Web.Security;
using SistemaSafeWork.Models;
class AuthIdentityRegression
{
 static string bin;
 static int Main(string[] args)
 {
  bin=args[0]; AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>Assembly.LoadFrom(Path.Combine(bin,new AssemblyName(e.Name).Name+".dll"));
  try { Run(); return 0; } catch(Exception e) { Console.Error.WriteLine(e.Message); return 1; }
 }
 static void Assert(bool condition,string label) { if(!condition)throw new Exception(label);Console.WriteLine("PASS: "+label); }
 static void Run()
 {
  Assert(Access.UserId(new Context(new GenericPrincipal(new GenericIdentity("sespana","Windows"),new string[0])))==0,"La identidad de dominio no identifica un Usuario");
  Assert(Access.UserId(new Context(new GenericPrincipal(new GenericIdentity("1","Windows"),new string[0])))==0,"Incluso un nombre Windows numérico se rechaza");
  Assert(Access.UserId(new Context(new GenericPrincipal(new FormsIdentity(new FormsAuthenticationTicket("1",false,30)),new string[0])))==1,"Un ticket Forms con ID de Usuario se acepta");
  Assert(Access.UserId(new Context(new GenericPrincipal(new FormsIdentity(new FormsAuthenticationTicket("sespana",false,30)),new string[0])))==0,"Una sesión Forms anterior con nombre se rechaza");
  Assert(Access.UserId(new Context(new GenericPrincipal(new FormsIdentity(new FormsAuthenticationTicket("0",false,30)),new string[0])))==0,"Un ID no válido se rechaza");
 }
 class Context:HttpContextBase
 { readonly IPrincipal principal;public Context(IPrincipal principal){this.principal=principal;}public override IPrincipal User {get{return principal;}set{throw new NotSupportedException();}} }
}
