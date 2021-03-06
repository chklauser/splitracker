import {FunctionComponent} from "react";
import {PointsBlock} from "../PointsBlock";
import {PointsVec} from "../char";
import {useDrag} from "react-dnd";
import {ItemTypes, PointsPreviewData} from "../drag";
import {classSet} from "../ClassSet";
import './PointsPreview.scss';

export interface IPointsPreviewProps {
  lineCapacity: number;
  totalCapcity: number;
  points: PointsVec;
  totalCurrentPoints: number;
  onAppliedPoints: (points: PointsPreviewData) => void;
  showPenalties: boolean;
}

export const PointsPreview: FunctionComponent<IPointsPreviewProps> = ({
  lineCapacity,
  totalCapcity,
  points,
  totalCurrentPoints,
  onAppliedPoints,
  showPenalties
}) => {
  const [{isDragging}, drag] = useDrag<PointsPreviewData, object, { isDragging: boolean }>(() => ({
    type: ItemTypes.PointsPreview,
    canDrag: points.consumed + points.exhausted + points.channeled != 0,
    end: (data: PointsPreviewData, monitor) => {
      if (monitor.didDrop()) {
        onAppliedPoints(data);
      }
    },
    item: monitor => ({points}),
    collect: monitor => ({
      isDragging: monitor.isDragging()
    })
  }), [points, onAppliedPoints]);
  return <div ref={drag} className={classSet({
    PointsPreview: true,
    btn: true,
    "btn-primary": true,
    "PointsPreview-dragging": isDragging
  })}>
    <PointsBlock {...{lineCapacity, totalCapcity, points, showPenalties}}
                     numSkip={totalCurrentPoints} hideEmptyLines={true} highlightDelta={true}/>
  </div>;
};