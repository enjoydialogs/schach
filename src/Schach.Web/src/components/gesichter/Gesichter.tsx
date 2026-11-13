import { Auge, AugeZu, Kopf, MUNDRAUM, Nase, Rahmen, Rumpf, Strich, TINTE, Wangen } from './teile'

/*
 * Die acht Gegner-Gesichter (SCHACH-SPEC 4.4). Jedes zeigt die Persoenlichkeit aus
 * `prompts/gegner/*.de.md` ueber Frisur, Brauen, Augen und Mund; die Silhouette (Dutt, Stacheln,
 * Glatze, Lockenwolke, Brille, Doppeldutt, schraeger Pony) unterscheidet sie auch bei 40 px.
 */

/** Ilse: Rentnerin aus Bayern, gemuetlich. Grauer Dutt, geschlossene laechelnde Augen, Trachtenbluse. */
export function GesichtIlse() {
  const haut = '#f4c09d'
  const haar = '#e4e0ec'
  return (
    <Rahmen hintergrund="#ffd166">
      <ellipse cx="50" cy="46" rx="31" ry="29" fill={haar} />
      <circle cx="50" cy="15" r="11" fill={haar} />
      <rect x="40" y="22" width="20" height="4" rx="2" fill="#c43c5a" />
      <Kopf
        haut={haut}
        kleidung={
          <>
            <Rumpf farbe="#3f9a6b" />
            <path d="M36 80 L50 92 L64 80 Q50 76 36 80 Z" fill="#ffffff" />
            <circle cx="50" cy="94" r="2.6" fill="#c43c5a" />
          </>
        }
      />
      <path d="M24 48 Q22 22 50 21 Q78 22 76 48 Q72 32 58 31 Q52 36 50 30 Q48 36 42 31 Q28 32 24 48 Z" fill={haar} />
      <circle cx="24" cy="61" r="2.8" fill="#ffffff" />
      <circle cx="76" cy="61" r="2.8" fill="#ffffff" />
      <Strich d="M33 40 Q39 37 45 40" farbe="#a29cae" breite={3} />
      <Strich d="M55 40 Q61 37 67 40" farbe="#a29cae" breite={3} />
      <AugeZu x={39} y={50} />
      <AugeZu x={61} y={50} />
      <Wangen deckkraft={0.65} />
      <Nase farbe="#e09f7c" />
      <Strich d="M42 66 Q50 72 58 66" />
    </Rahmen>
  )
}

/** Finn: Schuljunge, aggressiv und vorlaut. Rote Stachelhaare, angriffslustige Brauen, breites Grinsen. */
export function GesichtFinn() {
  const haut = '#f6c8a2'
  return (
    <Rahmen hintergrund="#5ec8ff">
      <Kopf
        haut={haut}
        kleidung={
          <>
            <Rumpf farbe="#ff4d4d" />
            <path d="M34 80 Q50 90 66 80 Q50 74 34 80 Z" fill="#d93636" />
            <Strich d="M44 86 L43 96" farbe="#ffffff" breite={2.4} />
            <Strich d="M56 86 L57 96" farbe="#ffffff" breite={2.4} />
          </>
        }
      />
      <path
        d="M24 46 L20 28 L31 30 L30 13 L42 22 L50 6 L57 21 L70 11 L70 27 L82 26 L76 46 Q70 32 52 31 Q34 31 24 46 Z"
        fill="#f0782c"
      />
      <Strich d="M31 38 L46 43" breite={4.6} />
      <Strich d="M69 38 L54 43" breite={4.6} />
      <Auge x={39} y={50} haut={haut} blickX={1.8} />
      <Auge x={61} y={50} haut={haut} blickX={1.8} />
      <g fill="#d9844f">
        <circle cx="35" cy="60" r="1.4" />
        <circle cx="39" cy="62" r="1.4" />
        <circle cx="34" cy="64" r="1.4" />
        <circle cx="65" cy="60" r="1.4" />
        <circle cx="61" cy="62" r="1.4" />
        <circle cx="66" cy="64" r="1.4" />
      </g>
      <Nase farbe="#e6a27c" />
      <path d="M37 63 Q50 81 63 63 Z" fill={MUNDRAUM} />
      <path d="M38.5 63.5 L61.5 63.5 Q60 67.5 50 67.5 Q40 67.5 38.5 63.5 Z" fill="#ffffff" />
    </Rahmen>
  )
}

/** Oscar: Grundschullehrer, freundlich-lehrerhaft. Seitenscheitel, Vollbart, offenes Lachen, Strickjacke. */
export function GesichtOscar() {
  const haut = '#c98b62'
  const haar = '#4a2f22'
  return (
    <Rahmen hintergrund="#7fe0b4">
      <Kopf
        haut={haut}
        kleidung={
          <>
            <Rumpf farbe="#f2b134" />
            <path d="M38 79 L50 90 L62 79 Q50 75 38 79 Z" fill="#ffffff" />
            <rect x="64" y="86" width="3" height="11" rx="1.2" fill="#e8463c" />
          </>
        }
      />
      <path d="M24 48 Q21 18 50 18 Q80 18 76 48 Q74 30 60 28 Q44 26 36 34 Q28 36 24 48 Z" fill={haar} />
      <path d="M25 54 Q27 82 50 82 Q73 82 75 54 Q72 66 63 67 Q50 62 37 67 Q28 66 25 54 Z" fill={haar} />
      <Strich d="M32 40 Q39 35 46 39" farbe={haar} breite={4} />
      <Strich d="M54 39 Q61 35 68 40" farbe={haar} breite={4} />
      <Auge x={39} y={50} haut={haut} lid={0.2} blickY={0.5} />
      <Auge x={61} y={50} haut={haut} lid={0.2} blickY={0.5} />
      <Nase farbe="#b07450" />
      <path d="M41 65 Q50 76 59 65 Z" fill={MUNDRAUM} />
      <path d="M42.5 65.5 L57.5 65.5 Q56 68.5 50 68.5 Q44 68.5 42.5 65.5 Z" fill="#ffffff" />
    </Rahmen>
  )
}

/** Nele: Studentin, verspielt und neckend. Lila Doppeldutt, Zwinkern, Zunge raus, Stern auf dem Pulli. */
export function GesichtNele() {
  const haut = '#f2b994'
  const haar = '#6b3fa0'
  return (
    <Rahmen hintergrund="#ff9fd0">
      <circle cx="27" cy="22" r="11" fill={haar} />
      <circle cx="73" cy="22" r="11" fill={haar} />
      <Kopf
        haut={haut}
        kleidung={
          <>
            <Rumpf farbe="#1fb5a8" />
            <path d="M72 84 L74 89 L79 89 L75 92 L77 97 L72 94 L67 97 L69 92 L65 89 L70 89 Z" fill="#ffd23f" />
          </>
        }
      />
      <path d="M24 48 Q23 20 50 20 Q77 20 76 48 L71 36 L64 42 L58 31 L50 40 L42 31 L36 42 L29 36 Z" fill={haar} />
      <Strich d="M33 43 Q39 40 45 42" breite={3.6} />
      <Strich d="M55 38 Q61 34 67 37" breite={3.6} />
      <AugeZu x={39} y={51} />
      <Auge x={61} y={50} haut={haut} blickX={-1.6} blickY={-0.8} />
      <Wangen />
      <Nase farbe="#e09c78" />
      <path d="M40 63 Q50 75 60 63 Z" fill={MUNDRAUM} />
      <ellipse cx="54" cy="71" rx="5" ry="5.5" fill="#ff6f8e" />
      <Strich d="M54 68 L54 73" farbe="#e0506f" breite={1.6} />
    </Rahmen>
  )
}

/** Theo: Zahlenmensch, vorsichtig. Glatze mit Haarkranz, besorgte Brauen, Seitenblick, Wellenmund, Pullunder. */
export function GesichtTheo() {
  const haut = '#eaa57f'
  const haar = '#8a8079'
  return (
    <Rahmen hintergrund="#f0d9a8">
      <Kopf
        haut={haut}
        kleidung={
          <>
            <Rumpf farbe="#5c6b8a" />
            <path d="M40 79 L50 88 L60 79 Q50 76 40 79 Z" fill="#ffffff" />
            <path d="M48 84 L52 84 L54 98 L50 100 L46 98 Z" fill="#b33a3a" />
          </>
        }
      />
      <ellipse cx="25.5" cy="43" rx="5.5" ry="10" fill={haar} />
      <ellipse cx="74.5" cy="43" rx="5.5" ry="10" fill={haar} />
      <ellipse cx="40" cy="31" rx="7" ry="3.5" fill="#ffffff" opacity="0.35" />
      <Strich d="M32 42 L46 37" breite={3.8} />
      <Strich d="M54 37 L68 42" breite={3.8} />
      <Auge x={39} y={51} haut={haut} lid={0.3} blickX={-2.4} />
      <Auge x={61} y={51} haut={haut} lid={0.3} blickX={-2.4} />
      <Nase farbe="#d48a64" />
      <Strich d="M42 67 Q46 64 50 67 Q54 70 58 67" breite={3} />
    </Rahmen>
  )
}

/** Sementa: Theaterfrau, eitel. Grosse Lockenwolke, Schlafzimmerblick mit Wimpern, roter Mund, Federboa. */
export function GesichtSementa() {
  const haut = '#b9744c'
  const haar = '#2a1a2e'
  return (
    <Rahmen hintergrund="#cf9bff">
      <ellipse cx="50" cy="44" rx="40" ry="35" fill={haar} />
      <circle cx="16" cy="58" r="10" fill={haar} />
      <circle cx="84" cy="58" r="10" fill={haar} />
      <Kopf haut={haut} kleidung={<Rumpf farbe="#b0186e" />} />
      <g fill="#ffc2e2">
        <circle cx="22" cy="88" r="7" />
        <circle cx="32" cy="84" r="7" />
        <circle cx="42" cy="82" r="6.5" />
        <circle cx="58" cy="82" r="6.5" />
        <circle cx="68" cy="84" r="7" />
        <circle cx="78" cy="88" r="7" />
      </g>
      <path d="M24 52 Q20 16 54 16 Q80 18 78 42 Q66 26 46 30 Q32 34 24 52 Z" fill={haar} />
      <circle cx="24" cy="64" r="4" fill="none" stroke="#ffd23f" strokeWidth="2.4" />
      <circle cx="76" cy="64" r="4" fill="none" stroke="#ffd23f" strokeWidth="2.4" />
      <Strich d="M32 41 Q38 34 46 38" breite={2.6} />
      <Strich d="M54 38 Q62 34 68 41" breite={2.6} />
      <Auge x={39} y={50} haut={haut} lid={0.5} blickX={1.6} />
      <Auge x={61} y={50} haut={haut} lid={0.5} blickX={1.6} />
      <Strich d="M32.5 50 L29.5 47.5 M33 47 L31 44" breite={2} />
      <Strich d="M67.5 50 L70.5 47.5 M67 47 L69 44" breite={2} />
      <Nase farbe="#9f5f3c" />
      <path d="M41 65 Q46 62 50 64 Q54 61 60 63 Q57 70 50 70 Q43 70 41 65 Z" fill="#e0245e" />
      <circle cx="64" cy="61" r="1.4" fill={TINTE} />
    </Rahmen>
  )
}

/** Konrad: pensionierter Mathelehrer, trocken-ironisch. Eckige Brille, hochgezogene Braue, Fliege, Sakko. */
export function GesichtKonrad() {
  const haut = '#f0b995'
  const haar = '#b9b6c0'
  return (
    <Rahmen hintergrund="#c8e46e">
      <Kopf
        haut={haut}
        kleidung={
          <>
            <Rumpf farbe="#8a5a3c" />
            <path d="M40 79 L50 96 L60 79 Q50 76 40 79 Z" fill="#ffffff" />
            <path d="M41 80 L50 84 L59 80 L59 89 L50 85 L41 89 Z" fill="#e0303a" />
          </>
        }
      />
      <path d="M24 50 Q22 30 30 26 Q34 34 30 50 Z" fill={haar} />
      <path d="M76 50 Q78 30 70 26 Q66 34 70 50 Z" fill={haar} />
      <path d="M40 24 Q50 16 60 24 Q50 22 40 24 Z" fill={haar} />
      <Strich d="M31 38 Q38 35 46 38" farbe="#8d8a94" breite={5} />
      <Strich d="M54 34 Q61 29 69 33" farbe="#8d8a94" breite={5} />
      <Auge x={39} y={50} haut={haut} rx={5.5} ry={6} lid={0.4} />
      <Auge x={61} y={50} haut={haut} rx={5.5} ry={6} lid={0.15} />
      <g fill="none" stroke={TINTE} strokeWidth="2.8">
        <rect x="29" y="42" width="20" height="15" rx="3" />
        <rect x="51" y="42" width="20" height="15" rx="3" />
        <path d="M49 47 L51 47" />
      </g>
      <Nase farbe="#dc9a76" />
      <Strich d="M43 67 Q52 70 59 63" />
    </Rahmen>
  )
}

/** Maren: Meisterin, aggressiv und vorlaut. Schraeger Bob, entschlossene Brauen, enger Blick, weinroter Rollkragen. */
export function GesichtMaren() {
  const haut = '#f3c4a0'
  const haar = '#2b1622'
  return (
    <Rahmen hintergrund="#ff7b6b">
      <path d="M20 76 Q14 20 50 16 Q86 20 80 76 Q74 78 70 72 L70 50 L30 50 L30 72 Q26 78 20 76 Z" fill={haar} />
      <Kopf
        haut={haut}
        kleidung={
          <>
            <Rumpf farbe="#8c1d3a" />
            <rect x="39" y="72" width="22" height="12" rx="5" fill="#8c1d3a" />
          </>
        }
      />
      <path d="M24 46 Q22 18 52 17 Q79 18 77 44 Q72 32 62 30 L36 39 Q29 39 24 46 Z" fill={haar} />
      <Strich d="M32 43 L46 47" breite={4.6} />
      <Strich d="M68 42 L54 46" breite={4.6} />
      <Auge x={39} y={52} haut={haut} lid={0.45} />
      <Auge x={61} y={52} haut={haut} lid={0.45} />
      <circle cx="75.5" cy="60" r="2" fill="#dfe3ea" />
      <Nase farbe="#e2a07c" />
      <Strich d="M43 67 Q52 69 59 62" />
    </Rahmen>
  )
}
