import {FunctionComponent} from "react";
import {PointsBlock, PointsVec} from "../PointsBlock";
import {useDrag} from "react-dnd";
import {ItemTypes, PointsPreviewData} from "../drag";
import {classSet} from "../ClassSet";
import './PointsPreview.css';

export interface IPointsPreviewProps {
  baseCapacity: number;
  points: PointsVec;
  totalCurrentPoints: number;
  onAppliedPoints: (points: PointsPreviewData) => void;
  showPenalties: boolean;
}

export const PointsPreview: FunctionComponent<IPointsPreviewProps> = ({
  baseCapacity,
  points,
  totalCurrentPoints,
  onAppliedPoints,
  showPenalties
}) => {
  const [{isDragging}, drag] = useDrag<PointsPreviewData,object,{isDragging: boolean}>(() => ({
    type: ItemTypes.PointsPreview,
    canDrag: points.consumed + points.exhausted + points.channeled != 0,
    end: (data: PointsPreviewData, monitor) => {
      if (monitor.didDrop()) {
        onAppliedPoints(data);
      }
    },
    item: monitor => ({ points }),
    collect: monitor => ({
      isDragging: monitor.isDragging()
    })
  }), [points, onAppliedPoints]);
  const hasHealing = points.consumed < 0 || points.exhausted < 0 || points.channeled < 0;
  const hasDamage = points.consumed > 0 || points.exhausted > 0 || points.channeled > 0;
  return <div ref={drag} className={classSet({PointsPreview: true, "PointsPreview-dragging": isDragging})}>
    <PointsBlock baseCapacity={baseCapacity} points={points} showPenalties={showPenalties}
                 numSkip={totalCurrentPoints} hideEmptyLines={true}/>
    {hasHealing && <div className="PointsPreview-healing">➕</div>}
    {hasDamage && <div className="PointsPreview-damage">➖</div>}
  </div>;
};