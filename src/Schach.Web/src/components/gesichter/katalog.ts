import { createElement, type ComponentType, type ReactElement } from 'react'
import {
  GesichtFinn,
  GesichtIlse,
  GesichtKonrad,
  GesichtMaren,
  GesichtNele,
  GesichtOscar,
  GesichtSementa,
  GesichtTheo,
} from './Gesichter'

/** Gesicht je Gegner-Key (`GegnerKatalog.cs`). Unbekannte Keys bekommen den Anfangsbuchstaben. */
export const GESICHTER: Readonly<Record<string, ComponentType>> = {
  ilse: GesichtIlse,
  finn: GesichtFinn,
  oscar: GesichtOscar,
  nele: GesichtNele,
  theo: GesichtTheo,
  sementa: GesichtSementa,
  konrad: GesichtKonrad,
  maren: GesichtMaren,
}

/** Gesicht als Element oder `undefined`, wenn der Key keins hat. */
export function gesichtFuer(key: string): ReactElement | undefined {
  const komponente = Object.hasOwn(GESICHTER, key) ? GESICHTER[key] : undefined
  return komponente === undefined ? undefined : createElement(komponente)
}
