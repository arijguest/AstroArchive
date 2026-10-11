using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroArchive {
 internal static class StartupLoadingTests {
  static void CheckDeck(string[] messages,int seed){
   if(messages.Any(string.IsNullOrWhiteSpace)||messages.Distinct().Count()!=messages.Length)throw new Exception("Empty or duplicate startup content.");
   var deck=new StartupMessageDeck(messages,new Random(seed));string previous=null;
   for(int cycle=0;cycle<6;cycle++){
    var seen=new HashSet<string>();
    for(int i=0;i<messages.Length;i++){string next=deck.Next();if(next==previous||!messages.Contains(next)||!seen.Add(next))throw new Exception("Startup deck repeated or lost a message.");previous=next;}
    if(seen.Count!=messages.Length)throw new Exception("Startup deck did not cover all messages.");
   }
  }
  public static int Main(){
   try{
    if(StartupMessages.Remarks.Length!=50)throw new Exception("Exactly 50 witty remarks are required.");
    if(StartupMessages.Facts.Length!=32)throw new Exception("Exactly 32 fun facts are required.");
    if(StartupMessages.Hints.Length<10||StartupMessages.Facts.Length<10||StartupMessages.Hints.Any(h=>!h.StartsWith("Did you know you can")))throw new Exception("Helpful hints and astrophotography facts are missing.");
    for(int seed=0;seed<100;seed++){CheckDeck(StartupMessages.Remarks,seed);CheckDeck(StartupMessages.Hints,seed);CheckDeck(StartupMessages.Facts,seed);}
    var first=new HashSet<string>();for(int seed=0;seed<100;seed++)first.Add(new StartupMessageDeck(StartupMessages.Remarks,new Random(seed)).Next());
    if(first.Count<15)throw new Exception("Startup remarks are not varied across launches.");
    Console.WriteLine("PASS: 50 remarks, 12 hints, 32 facts; shuffled coverage, no consecutive repeats and varied launches.");return 0;
   }catch(Exception error){Console.Error.WriteLine(error);return 1;}
  }
 }
}
