using System;

namespace AstroArchive {
 // Independent shuffled decks keep both the humour and the useful notes fresh.
 internal sealed class StartupMessageDeck {
  readonly Random random;readonly string[] values;int next;string previous;
  public StartupMessageDeck(string[] messages,Random random){if(messages==null||messages.Length<2)throw new ArgumentException("At least two messages are required.");this.random=random;values=(string[])messages.Clone();next=values.Length;}
  public string Next(){
   if(next==values.Length){for(int i=values.Length-1;i>0;i--){int j=random.Next(i+1);string value=values[i];values[i]=values[j];values[j]=value;}if(values[0]==previous){string value=values[0];values[0]=values[1];values[1]=value;}next=0;}
   previous=values[next++];return previous;
  }
 }
 internal static class StartupMessages {
  public static readonly string[] Remarks={
   "Annihilating clouds…",
   "Wishing you a clear night",
   "Negotiating with the weather forecast…",
   "Politely asking the Moon to dim…",
   "Herding photons into tidy folders…",
   "Giving your galaxies some personal space…",
   "Convincing the clouds you bought no new gear…",
   "Aligning the stars. And the filenames…",
   "Checking that Orion still has his belt…",
   "Putting the Rosette in the right vase…",
   "Making room for just one more telescope…",
   "Asking Saturn to hold onto its rings…",
   "Telling the dew to take the night off…",
   "Finding the bright side of your dark frames…",
   "Reminding the tripod to stand still…",
   "Untangling cables across the cosmos…",
   "Helping your subs find their stackmates…",
   "Waiting for the universe to say cheese…",
   "Keeping the Horsehead out of the house…",
   "Resisting the urge to buy another filter…",
   "Counting stars. Losing count. Starting again…",
   "Giving Andromeda a little breathing room…",
   "Packing a thermos for the next clear night…",
   "Persuading the neighbours to switch off their lights…",
   "Checking the focus. Just one more time…",
   "Keeping the dark frames in the dark…",
   "Preparing for a very late bedtime…",
   "Asking the Pleiades to stay together…",
   "Turning cosmic chaos into a collection…",
   "Saving your place among the stars…",
   "Hmm, where did I put that one?",
   "Opening final_final_really_final.fit…",
   "Looking for M31 under ‘miscellaneous’…",
   "Moving ‘sort these later’ slightly closer to later…",
   "Searching for the lens cap. It’s on the lens…",
   "Explaining to the cat that this is not a heated bed…",
   "Checking whether ‘just one more sub’ was a promise…",
   "Translating ‘five minutes’ from astronomer time…",
   "Finding a USB port that hasn’t been claimed…",
   "Wondering why the best frame was the test shot…",
   "Remembering the password to the telescope’s Wi-Fi…",
   "Admiring the galaxy behind your satellite trail…",
   "Reclassifying that meteor as an aeroplane…",
   "Convincing the hard drive that space is infinite…",
   "Locating the image you definitely didn’t delete…",
   "Trying to pronounce Zubenelgenubi…",
   "Discovering that the red torch has rolled away…",
   "Wondering if the smudge is dust or a discovery…",
   "Checking the histogram. Pretending to understand it…",
   "Organising the archive before it becomes a black hole…"
  };
  public static readonly string[] Hints={
   "Did you know you can press F1 to open AstroArchive’s offline guide?",
   "Did you know you can import from Seestar and DWARF over your local network?",
   "Did you know you can use live import to bring in completed captures while your telescope keeps shooting?",
   "Did you know you can run independent network imports from more than one telescope?",
   "Did you know you can browse your captures by target and session in Repository?",
   "Did you know you can prepare stacking folders with matched calibration frames for your processing software?",
   "Did you know you can keep working copies and processing results together in Edited?",
   "Did you know you can compare integration time, targets and telescope usage in Repository → Analytics?",
   "Did you know you can export your analytics as PNG, JPEG, PDF or SVG?",
   "Did you know you can export an animated analytics story as MP4 or GIF?",
   "Did you know you can create a verified folder or lossless ZIP backup of your archive?",
   "Did you know you can change the preview stretch without changing your original image bytes?"
  };
  public static readonly string[] Facts={
   "The Rosette Nebula is a stellar nursery roughly 5,000 light-years away in the constellation Monoceros.",
   "Light from the Andromeda Galaxy has travelled for about 2.5 million years before reaching your camera.",
   "The Orion Nebula is about 1,350 light-years away, and new stars are forming inside it.",
   "Doubling your signal-to-noise ratio generally takes four times the total exposure, under similar conditions.",
   "Hydrogen-alpha light has a wavelength of about 656.3 nanometres, in the red part of the visible spectrum.",
   "The Pleiades are an open star cluster. Their blue nebulosity comes from dust reflecting starlight.",
   "A light-year measures distance: the distance light travels in one year, about 9.46 trillion kilometres.",
   "Dark frames record a sensor’s dark current and fixed-pattern signals at a matched exposure and temperature.",
   "Flat frames help correct vignetting and dust shadows when captured with the same optical setup.",
   "Dithering shifts the framing between exposures, helping stacking reject hot pixels and reduce pattern noise.",
   "The Moon’s apparent width is about half a degree. Many deep-sky targets cover much more sky.",
   "The stars in a constellation can be at very different distances, even when they look like neighbours."
  };
 }
}
