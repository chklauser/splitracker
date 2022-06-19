import {FunctionComponent, ReactElement} from "react";
import {Point, PointDisplay} from "../PointDisplay";
import './PointsBlock.scss';
import {PointsVec} from "../char";

export interface IPointsBlockProps {
  lineCapacity: number;
  totalCapcity: number;
  points: PointsVec;
  showPenalties: boolean
  numSkip?: number
  hideEmptyLines?: boolean,
  highlightDelta?: boolean
}

type AnnotatedPoint = {
  point: Point;
  value: 1|0| -1;
};

export const PointsBlock: FunctionComponent<IPointsBlockProps> = ({
  showPenalties,
  points,
  lineCapacity,
  totalCapcity,
  numSkip = 0,
  hideEmptyLines = false,
  highlightDelta = false
}) => {
  let penaltiesCell: (rowIndex: number, active: boolean) => ReactElement<any, any> | null;
  if (showPenalties) {
    penaltiesCell = (rowIndex, active) => {
      const className = active ? "PointsBlock-active" : "PointsBlock-inactive";
      switch (rowIndex) {
        case 0:
          return <th className={className}>Unversehrt ±0</th>
        case 1:
          return <th className={className}>Angeschlagen -1</th>
        case 2:
          return <th className={className}>Verletzt -2</th>
        case 3:
          return <th className={className}>Schwer Verletzt -4</th>
        case 4:
          return <th className={className}>Todgeweiht -5</th>
        default:
          throw Error("Invalid rowIndex for showPenalties (0..4): " + rowIndex);
      }
    };
  } else {
    penaltiesCell = () => null;
  }

  const norm: PointsVec = {
    channeled: Math.abs(points.channeled),
    exhausted: Math.abs(points.exhausted),
    consumed: Math.abs(points.consumed)
  };

  const effSkip = numSkip
    + (points.channeled < 0 ? points.channeled : 0)
    + (points.exhausted < 0 ? points.exhausted : 0)
    + (points.consumed < 0 ? points.consumed : 0);

  const virtualCapacity = Math.ceil(totalCapcity / lineCapacity)*lineCapacity;
  const cells: AnnotatedPoint[] = Array.from(new Array(virtualCapacity ), (x, i) =>
    i < effSkip ? { point: Point.Free, value: 0 }
      : i - effSkip < norm.consumed ? { point: Point.Consumed, value: points.consumed > 0 ? 1 : -1 }
        : i - effSkip - norm.consumed < norm.exhausted ? { point: Point.Exhausted, value: points.exhausted > 0 ? 1 : -1 }
          : i - effSkip - norm.consumed - norm.exhausted < norm.channeled ? { point: Point.Channeled, value: points.channeled > 0 ? 1 : -1 }
            : { point: Point.Free, value: 0 });
  const rows = cells.reduce((resultArray: AnnotatedPoint[][], item, index) => {
    const chunkIndex = Math.floor(index / lineCapacity)

    if (!resultArray[chunkIndex]) {
      resultArray[chunkIndex] = [] // start a new chunk
    }

    resultArray[chunkIndex].push(item)

    return resultArray
  }, []);


  function hasPoints(row: AnnotatedPoint[]) {
    return row.map(p => p.point != Point.Free).reduce((l, r) => l || r);
  }

  return <div className="PointsBlock">
    <table className="PointsBlock-table table table-sm">
      <colgroup>
        { showPenalties ? <col className="PointsBlock-levelCol" /> : null }
        {Array.from(new Array(lineCapacity), (_,i) => <col key={i} className="PointsBlock-pointCol" />)}
      </colgroup>
      <tbody>
      {rows.map((row, rowIndex) =>
        !hideEmptyLines || hasPoints(row) ?
          <tr key={rowIndex}>
            {penaltiesCell(rowIndex, hasPoints(row))}
            {row.map((cell, cellIndex) =>
              <td key={`${rowIndex}-${cellIndex}`}>
                <PointDisplay {...cell} highlightDelta={highlightDelta} />
              </td>
            )}
          </tr> : null
      )}
      </tbody>
    </table>
  </div>;
};