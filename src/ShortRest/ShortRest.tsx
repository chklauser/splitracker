import {FunctionComponent} from "react";
import {PointsVec} from "../char";
import {useDrag} from "react-dnd";
import {ItemTypes, PointsPreviewData} from "../drag";
import {classSet} from "../ClassSet";
import "./ShortRest.css";

export interface IShortRestProps {
  currentPoints: PointsVec;
}

export const ShortRest: FunctionComponent<IShortRestProps> = ({currentPoints}) => {
  const [{isDragging}, drag] = useDrag<PointsPreviewData, object, { isDragging: boolean }>(() => ({
    type: ItemTypes.PointsPreview,
    canDrag: currentPoints.exhausted > 0,
    item: monitor => ({points: {
      exhausted: -currentPoints.exhausted,
      channeled: 0,
      consumed: 0
    }}),
    collect: monitor => ({
      isDragging: monitor.isDragging()
    })
  }), [currentPoints]);
  const pointsHealed = currentPoints.exhausted;
  return currentPoints.exhausted > 0 ? <div ref={drag} className={classSet({ShortRest: true, "ShortRest-dragging": isDragging})}>
    <p className="ShortRest-title">Verschnaufpause 😮‍💨</p>
    {pointsHealed > 0 ? <p className="ShortRest-points">Heile erschöpfte Punkte: {pointsHealed}</p> :
      <p>Kein Effekt</p>}
  </div> : null;
};