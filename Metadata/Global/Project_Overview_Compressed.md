S C R F - M T D T (SoftwareCenterRefactored Metadata)
1. P R J C T S
N M|T Y P|F N C|D P N D S|F L D R
C N T R C T S|.NET S T N D R D|I N T E R F A C E S / D T O S|N O N E|C O R E /
H O S T|ASP.NET C O R E|UI; B S C S V C; M D L M N G|K R N L, L A N C H R, I S V C|H O S T /
K R N L|.NET S T N D R D|S M R T R T R; S V C R G Y; G L B L D T S|N O N E|K E R N E L /
L A N C H R|.NET X C|W D G; P R C S S M N G; R N C T R L|H O S T, K R N L|L A N C H R /
I S V C|.NET S V C|R M T P S H I N S T L; A D M N L V L|K R N L, I N S T L L R M D L|I N S T L S V C /
M D L . A P P M N G|.NET S T N D R D|A P P L C Y C L; I N V E N T R Y M R G|K R N L|M O D U L E S / A P P M N G R /
M D L . S R C M N G|.NET S T N D R D|R E M T / L C L F I L E C R U D|K R N L|M O D U L E S / S R C M N G R /
M D L . C R D M N G|.NET S T N D R D|W C M / D B C R E D S|K R N L|M O D U L E S / C R D M N G R /
M D L . L G G N O T|.NET S T N D R D|S E R I L O G; C M P L X N T F C|K R N L|M O D U L E S / L G G R N O T /
M D L . D B M N G|.NET S T N D R D|S Q L / C A C H E / H I S T R Y|K R N L|M O D U L E S / D B M N G R /
M D L . A I . A G N T|.NET S T N D R D|C O P I L O T / A Z U R E A I|K R N L|M O D U L E S / A I. A G E N T /
2. K R N L C N T R C T S (Generic Only - Now in Contracts)
I N T R F C|F N C|C M P R S S D S G N A T U R E
I M D L|C O R E M D L D F N|R G S T R (I K R N L): V O I D
I C M D|G E N E R I C M S G|N A M E: S T R; P A R A M S: D I C T
I R E S U L T|G E N E R I C R E T|S U C C E S S: B O O L; D A T A: O B J
I H N D L R|C M D E X E C|E X E C (I C M D): T A S K < I R E S U L T >
I R E G I S T R Y|B U S L O G I C|R E G ( N A M E , H N D L R , P R I O )
I U I D E F|A B S T R A C T U I|C O N T E N T P A R T ( H E A D , B O D Y , F O O T )
3. D R C T R Y M P (Standardized Folder Structure Map)
P R J C T|D R C T R Y M P
C N T R C T S|P R J M T D T, S R C ( I N T E R F A C E S )
H O S T|C N F G, L G C, M D L W R, P R J M T D T, S V C S, W W R T
K R N L|C O R E, D T, L G C, P R J M T D T, S V C S
L A N C H R|C N F G, L G C, P R J M T D T, S V C S
I S V C|C N F G, P R J M T D T, P R T C L, S V C S
M D L S|I N T R F C S, M D L S, P R J M T D T, S V C S, U I >> H T M L / S C R P T / S T Y L E
4. D P N D C N T R C T S (Dependency Contracts)
C N T R C T|R E Q U I R E M E N T
C O R E|A L L P R O J E C T S M U S T R E F E R E N C E S O F T W A R E C E N T E R . C O N T R A C T S.
H O S T|N O R E F E R E N C E T O K E R N E L O R M O D U L E S. L O A D S V I A R E F L E C T I O N.
K E R N E L|N O R E F E R E N C E T O H O S T. I M P L E M E N T S C O R E I N T E R F A C E S.
5. R U L E S (Global Design & Development Policies)
R U L E N M|D S C R P T N
R U L E - 0 1|C H N G E S ( S M L : 1 G O, L R G : B T C H ) M U S T F O L L O W P L N / O U T L N / D T L S A P P R O V E D P R I O R C O D I N G.
R U L E - 0 2|A L L P H A S E S M U S T E N D W I T H F U L L Y I M P L M E N T D F N C ( N O T O D O : I M P L E M E N T S T U B S ).|||
R U L E - 0 3|D E L E T E D C O D E M U S T B E C O M M E N T E D O U T W I T H C O M M E N T S ( W H Y / T M S T M P / T A G ) F O R R O L L B A C K.
R U L E - 0 4|E V E R Y P R J C T M U S T H A V E A P R O P E R V R S N.
R U L E - 0 5|C O D E R E S P N S E S M U S T E X P L C T L Y P O I N T O U T O M I S S I O N S T O P R E V E N T U N W N T D C H N G E S.
R U L E - 0 6|C H N G E S M U S T E X P L C T L Y C I T E P R J C T / F I L E / F N C N M E F O R P R C S E L O C A T I O N.
R U L E - 0 7|A L L C O D E M U S T B E O R G N Z D I N T O S C T N S / R G N S F O L L O W I N G C O N S S T E N T O R D E R.
R U L E - 0 8|K R N L M U S T I M P L M E N T A S P E C I A L R O U T E F O R R T M E A P I D O C S ( M D L S / R O U T E S / S V C S ).
R U L E - 0 9|T H E I D E A S D O C M N T M U S T S P P R T E A S Y S R C H / F L T R B Y P R J C T / F E A T U R E.
R U L E - 1 0|E A C H P R J C T M U S T H A V E I T S O W N H S T R Y L O G I N P R J M T D T / P R J _ H I S T _ L O G . M D.
R U L E - 1 1|A P I / C M D C N T R C T S ( I N K R N L ) M U S T O N L Y B E M D I F I E D B Y A D D I N G N E W M E M B E R S. E X I S T I N G M E M B E R S C A N N O T B E R M V D O R C H N G D W I T H O U T D E P R C A T I O N P R O T O C O L S.
R U L E - 1 2|A N Y P R J C T S T R C T R C H N G E M U S T U P D T D R C T R Y M P S C T N.
R U L E - 1 3|E A C H P R J C T H S T R Y L O G M U S T B E I N P R J M T D T / P R J _ H I S T _ L O G . M D.
R U L E - 1 4|D A T A E X C H N G M U S T U S E D I C T I O N A R Y < S T R , O B J > F O R E X T E N S I B I L I T Y & S A F E T Y.
R U L E - 1 5|C O N F I G I S S E L F - M N G D B Y M D L S I N T H E I R O W N F L D R S. H O S T M N G S O N L Y G L O B A L S T T N G S.
R U L E - 1 6|U I I S T E M P L A T E - B S D. H O S T U S E S R A W H T M L T M P L S T O R N D R A B S T R C T M D L C O N T N T.
R U L E - 1 7|E X C P T N B A R R I E R : K R N L M U S T W R A P A L L M D L C A L L S I N T R Y - C A T C H T O T R I G G E R F L L B K, N E V E R C R A S H.
R U L E - 1 8|D O N O T C R A S H : A L W A Y S H N D L E E R R S G R C F L L Y W I T H U I F E E D B A C K.
R U L E - 1 9|P R E - F L I G H T : E V E R Y P R J C T M U S T C R E A T E D O C S ( P R J _ S T R C T, P R J _ P L A N, P R J _ H I S T ) I N P R J M T D T B E F O R E C O D E.
R U L E - 2 0|D O C S Y N C : U P D A T E T H E 3 D O C S A T S T A R T & E N D O F E V E R Y P H A S E.
R U L E - 2 1|G R E E N B U I L D : P R F R K E E P I N G C O D E C M P I L B L E A F T R E V R Y C H N G.
R U L E - 2 2|C O M M A N D B U S : N O B I Z I N T E R F A C E S I N K R N L. U S E S T R I N G C M D S & D I C T P A R A M S O N L Y.
R U L E - 2 3|5 Z O N E S : H O S T M U S T P R O V I D E : T I T L E , N O T I F , P O W E R , N A V , C O N T E N T.
R U L E - 2 4|R E A C T I V E U I : C O N T R O L S U S E B I N D K E Y. H O S T P U S H E S S H A D O W S T A T E U P D A T E S V I A S I G N A L R.
R U L E - 2 5|S T Y L E A P I : H O S T E X P O S E S C S S T O K E N S V I A A P I F O R R U N T I M E D I S C O V E R Y.
R U L E - 2 6|A S Y N C F I R S T : A L L C M D S / H A N D L E R S M U S T B E T A S K < T > T O E N S U R E N O N - B L O C K I N G U I.
R U L E - 2 7|S H A D O W S T A T E : H O S T M A I N T A I N S D I C T O F U I V A L U E S F O R R E - H Y D R A T I N G H I D D E N V I E W S.
R U L E - 2 8|L O G M A T R I X : L O G S H A V E L E V E L ( I N F O / W A R N ) + V E R B O S I T Y ( I M P R T N T / V E R B O S E ).
R U L E - 2 9|M I C R O K E R N E L : H O S T L O A D S K E R N E L O N L Y. K E R N E L L O A D S A L L M O D U L E S. H O S T I S S A F E W / O K E R N E L.
6. A P I M A P & C M D S
R O U T E|C M D N M|D F L T H N D L R|F L B K|C R D M D L O V R D S
/ A P I / S R C / C P Y|C P Y S R C C M D|H O S T|O K|M D L . S R C M N G
/ A P I / C R D / S V|S V C R D C M D|H O S T|O K|M D L . C R D M N G
/ A P I / L G / M S G|L G M S G C M D|H O S T|O K|M D L . L G G N O T
/ A P I / M D L / C O M S|G T C O M S C M D|K R N L|N / A|N O N E
/ A P I / D O C S|G T D O C C M D|K R N L|N / A|N O N E
7. G L B L D T S K E Y S
K E Y|D T T Y P|F N C
A P P . V S N|S T R N G|C U R R E N T A P P V R S N
C M D . F A I L S|I N T E G E R|C N T R F R C M D F A I L R S
U I . T H M|E N U M|A C T V E U I T H E M E
8. L G N D S, N T T N S & T H R X P L N T N S
N T T N|X P L N T N
P R J M T D T|Project Metadata (Project-Specific Folder)
H S T R Y L O G|History Log (Project_History_Log.md file)
P R E - F L I G H T|Pre-Flight Check (Mandatory Steps Before Start)
D O C S Y N C|Documentation Synchronization
G R E E N B U I L D|Preference for working build state
C O M M A N D B U S|Architecture using string-based commands only
5 Z O N E S|Mandatory UI Zones (Title, Notif, Power, Nav, Content)
R E A C T I V E|Real-time UI updates via BindKey
D S G N T K N S|CSS Variables for Theming (Design Tokens)
S T Y L E A P I|Runtime discovery of CSS Tokens
A S Y N C F I R S T|All commands must be asynchronous
S H A D O W S T A T E|In-memory persistence for UI re-hydration
L O G M A T R I X|Separation of Log Level and Verbosity
D R C T R Y M P|Directory Map (Standardized Folder Structure)
C N F G|Config (Configuration files)
L G C|Logic (Core executable flow/startup)
M D L W R|Middleware (Web application pipeline components)
S V C S|Services (Service implementations/API handlers)
W W R T|wwwroot (Static web assets)
P R T C L|Protocol (Communication formats/DTOs)
C N T R C T S|Contracts (Interfaces/Abstractions)
C O R E|Core (Smart Router/Kernel core implementation)
D T|Data (Global Data Store implementation)
M D L S|Models (Data Transfer Objects/Entities - in Modules)
I N T R F C S|Interfaces (Interfaces specific to the Module)
U I|User Interface (Module UI container)
H T M L / S C R P T / S T Y L E|UI Subfolders (Raw HTML, JavaScript, CSS)
R U L E S|Global Design & Development Policies
V R S N|Version
H S T R Y L O G|History Log (Project-Specific Metadata)
C N S S T E N T|Consistent
P R C S E|Precise
D O C S|Documentation
