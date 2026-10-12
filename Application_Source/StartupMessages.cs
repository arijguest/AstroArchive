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
   "Looking for the galaxy you saved as ‘test.fit’…",
   "Convincing the clouds you bought no new gear…",
   "Aligning the stars. And the filenames…",
   "Framing Orion without cutting off his belt…",
   "Finding the Rosette among fifty ‘untitled’ stacks…",
   "Making room for just one more telescope…",
   "Keeping Saturn’s rings inside the frame…",
   "Telling the dew to take the night off…",
   "Finding the bright side of your dark frames…",
   "Reminding the tripod to stand still…",
   "Untangling cables across the cosmos…",
   "Helping your subs find their stackmates…",
   "Teaching the shutter the meaning of ‘bulb’…",
   "Rescuing the Horsehead from ‘New folder (7)’…",
   "Resisting the urge to buy another filter…",
   "Matching stars. Ignoring the hot pixels…",
   "Trying to fit Andromeda into one more mosaic panel…",
   "Finding the flats you took ‘somewhere sensible’…",
   "Persuading the neighbours to switch off their lights…",
   "Checking the focus. Just one more time…",
   "Explaining to the dark frames why the lens cap stays on…",
   "Converting ‘one more exposure’ into lost sleep…",
   "Fitting all the Pleiades into the same frame…",
   "Sorting FITS files before they form their own constellation…",
   "Bringing wandering subframes back to their session…",
   "Hmm, where did I put that one?",
   "Opening final_final_really_final.fit…",
   "Looking for M31 under ‘miscellaneous’…",
   "Moving ‘sort these subs later’ slightly closer to later…",
   "Searching for the lens cap. It’s on the lens…",
   "Asking the dew heater to warm the lens, not the cat…",
   "Checking whether ‘just one more sub’ was a promise…",
   "Translating ‘five minutes’ from astronomer time…",
   "Finding a USB port for the guide camera…",
   "Wondering why the best frame was the test shot…",
   "Asking the telescope’s Wi-Fi to stay for one more sub…",
   "Admiring the galaxy behind your satellite trail…",
   "Reclassifying that meteor as an aeroplane…",
   "Convincing the hard drive that space is infinite…",
   "Locating the image you definitely didn’t delete…",
   "Finding the light frame hiding among the darks…",
   "Checking the stars haven’t drifted out of the frame…",
   "Wondering if the smudge is dust or a discovery…",
   "Negotiating a truce with the background gradient…",
   "Organising the archive before it becomes a black hole…"
  };
  public static readonly string[] Hints={
   "Did you know you can press F1 to open AstroArchive’s offline guide?",
   "Did you know you can import from Seestar, DWARF and Celestron Origin over your local network?",
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
   "The stars in a constellation can be at very different distances, even when they look like neighbours.",
   "The Crab Nebula, M1, is the aftermath of a supernova recorded by astronomers in 1054.",
   "The Ring Nebula, M57, has a three-dimensional structure: a football-shaped cloud piercing a doughnut of gas.",
   "The Dumbbell Nebula, M27, was the first planetary nebula discovered, spotted by Charles Messier in 1764.",
   "The Veil Nebula’s delicate filaments are debris from a massive star that exploded thousands of years ago.",
   "The Eagle Nebula’s Pillars of Creation are towers of gas and dust where new stars are forming.",
   "The Horsehead Nebula is a dark cloud silhouetted against brighter, glowing gas behind it.",
   "The Whirlpool Galaxy, M51, is interacting with a smaller galaxy, helping trigger new star formation.",
   "The Sombrero Galaxy, M104, gets its hat-like appearance from a bright central bulge and a dark dust lane.",
   "The Hercules Cluster, M13, packs hundreds of thousands of stars into a ball held together by gravity.",
   "Omega Centauri looks like a fuzzy star to the naked eye, but contains millions of stars.",
   "The Cigar Galaxy, M82, is a starburst galaxy: interactions with nearby M81 fuel unusually rapid star formation.",
   "The Triangulum Galaxy, M33, is the Local Group’s third-largest galaxy, after Andromeda and the Milky Way.",
   "Sirius has a tiny companion, Sirius B, a white dwarf roughly the size of Earth.",
   "Polaris, our North Star, belongs to a triple-star system. Its brightest member is also a variable star.",
   "Betelgeuse is a red supergiant so large that, in the Sun’s place, it would extend beyond the orbit of Mars.",
   "The Double Cluster in Perseus is two neighbouring open star clusters, NGC 869 and NGC 884.",
   "Jupiter spins once in about ten hours, giving the Solar System’s biggest planet its shortest day.",
   "Saturn’s rings are made mostly of water ice, with pieces ranging from tiny grains to enormous chunks.",
   "Venus takes about 243 Earth days to spin once, longer than its 225-day journey around the Sun.",
   "Uranus is tipped almost onto its side, with an axial tilt of about 98 degrees."
  };
 }
}
