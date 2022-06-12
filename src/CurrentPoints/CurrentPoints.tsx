import {FunctionComponent} from "react";
import {IPointsBlockProps, PointsBlock} from "../PointsBlock";
import {useDrop} from "react-dnd";
import {ItemTypes, PointsPreviewData} from "../drag";
import {classSet} from "../ClassSet";
import './CurrentPoints.css';

export interface ICurrentPointsProps extends IPointsBlockProps {
  onReceivePoints: (points: PointsPreviewData) => void;
}

export const CurrentPoints: FunctionComponent<ICurrentPointsProps> = (props) => {
  const [{isOver}, drop] = useDrop<PointsPreviewData,object,{isOver: boolean}>({
    accept: ItemTypes.PointsPreview,
    canDrop: (item: PointsPreviewData) => {
      let delta = item.points.consumed + item.points.exhausted + item.points.channeled;
      return delta != 0
        && delta > 0 ? props.points.consumed + props.points.exhausted + props.points.channeled < props.baseCapacity * 5
        : props.points.consumed + props.points.exhausted + props.points.channeled > 0;
    },
    drop: (incomingPoints: PointsPreviewData) => {
      props.onReceivePoints(incomingPoints);
      return undefined;
    },
    collect: monitor => ({
      isOver: monitor.isOver()
    })
  }, [props.points, props.onReceivePoints]);
  return <div ref={drop} className={classSet({CurrentPoints: true, "CurrentPoints-over": isOver})}>
    <PointsBlock {...props} />
    {isOver && (
      <div className="CurrentPoints-dropOverlay">

      </div>
    )}
  </div>;
}