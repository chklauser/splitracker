# Punkte-Tracker für Splittermond

👉 [LIVE Version verwenden](https://splitracker.klauser.link/) 👈

# Updates

## 2022-06-19
* ⬅️➡️ undo/redo support for up to 10 actions
* ✨ Allow any number of focus points up to 60 
* ⌨️ `K3V2` expressions supported in the points field. <kbd>Enter</kbd> applies the points.
  * Expressions are read from left to right 
  * `K`/`E`/`V` (or `k`/`e`/`v`) changes the type of point
  * `+`/`-` changes whether points are added or subtracted
  * numbers are applied
  * other characters are ignored
  * Example: `K3v2` will
    * channel 3 points
    * of which 2 points are immediately consumed
  * Example: `k 3 2 v 3 - 1 E4 +1` will
    * channel 5 points (3 + 2)
    * of which 2 points (3 - 1) are immediately consumed
    * exhaust -3 points (-4 + 1) (negative = heal)
* (now with the "collapsing" interpretation of KEV formulas)
* 👨‍👩‍👧‍👦 Multiple characters
  * Add/remove/clone characters by going into "edit mode" (✏️-toggle in the top right corner)
  * Characters are always ordered alphabetically
  * Characters have a (hidden) identity
  * Characters have a name (that can be changed in edit mode)
  * Known issue: can drag channeling across characters. Don't do that pls ;) 

## 2022-06-18

* 💄 rendering & layout pass: should no longer scroll horizontally on mobile. 
* Use "proper" icons for K/E/V.
* swap preview & edit controls aroun (to make controls easier to reach on mobile)
* prevent LP/FO points from going below 1 (would crash app)

## 2022-06-14

* 🧠 Der Zustand des Charakters wird im Browser gespeichert. Übernehme keine Gewähr was passiert, wenn ihr mehrere
  Browser-Tabs öffnet (es gibt im Moment nur einen "Speicherplatz").
* Link zu GitHub unten links

## 2022-06-13

* 🖱️/👆 Toggle in der oberen rechten Ecke. Schaltet zwischen Maus- und Touch-Modus hin und her. Beeinflusst primär
  Drag&Drop. Das Setting wird im Browser gespeichert.
