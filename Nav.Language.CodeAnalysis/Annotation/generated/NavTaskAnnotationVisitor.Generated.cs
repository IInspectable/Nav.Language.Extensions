
namespace Pharmatechnik.Nav.Language.CodeAnalysis.Annotation {

    public interface INavTaskAnnotationVisitor {
        void VisitNavTaskAnnotation(NavTaskAnnotation navTaskAnnotation);
        void VisitNavChoiceAnnotation(NavChoiceAnnotation navChoiceAnnotation);
        void VisitNavExitAnnotation(NavExitAnnotation navExitAnnotation);
        void VisitNavInitAnnotation(NavInitAnnotation navInitAnnotation);
        void VisitNavInitCallAnnotation(NavInitCallAnnotation navInitCallAnnotation);
        void VisitNavTriggerAnnotation(NavTriggerAnnotation navTriggerAnnotation);
    }

    public interface INavTaskAnnotationVisitor<T> {
        T VisitNavTaskAnnotation(NavTaskAnnotation navTaskAnnotation);
        T VisitNavChoiceAnnotation(NavChoiceAnnotation navChoiceAnnotation);
        T VisitNavExitAnnotation(NavExitAnnotation navExitAnnotation);
        T VisitNavInitAnnotation(NavInitAnnotation navInitAnnotation);
        T VisitNavInitCallAnnotation(NavInitCallAnnotation navInitCallAnnotation);
        T VisitNavTriggerAnnotation(NavTriggerAnnotation navTriggerAnnotation);
    }


    partial class NavTaskAnnotation {

        internal virtual void Accept(INavTaskAnnotationVisitor visitor) {
            visitor.VisitNavTaskAnnotation(this);
        }

        internal virtual T Accept<T>(INavTaskAnnotationVisitor<T> visitor) {
            return visitor.VisitNavTaskAnnotation(this);
        }
    }

    partial class NavChoiceAnnotation {

        internal override void Accept(INavTaskAnnotationVisitor visitor) {
            visitor.VisitNavChoiceAnnotation(this);
        }

        internal override T Accept<T>(INavTaskAnnotationVisitor<T> visitor) {
            return visitor.VisitNavChoiceAnnotation(this);
        }
    }

    partial class NavExitAnnotation {

        internal override void Accept(INavTaskAnnotationVisitor visitor) {
            visitor.VisitNavExitAnnotation(this);
        }

        internal override T Accept<T>(INavTaskAnnotationVisitor<T> visitor) {
            return visitor.VisitNavExitAnnotation(this);
        }
    }

    partial class NavInitAnnotation {

        internal override void Accept(INavTaskAnnotationVisitor visitor) {
            visitor.VisitNavInitAnnotation(this);
        }

        internal override T Accept<T>(INavTaskAnnotationVisitor<T> visitor) {
            return visitor.VisitNavInitAnnotation(this);
        }
    }

    partial class NavInitCallAnnotation {

        internal override void Accept(INavTaskAnnotationVisitor visitor) {
            visitor.VisitNavInitCallAnnotation(this);
        }

        internal override T Accept<T>(INavTaskAnnotationVisitor<T> visitor) {
            return visitor.VisitNavInitCallAnnotation(this);
        }
    }

    partial class NavTriggerAnnotation {

        internal override void Accept(INavTaskAnnotationVisitor visitor) {
            visitor.VisitNavTriggerAnnotation(this);
        }

        internal override T Accept<T>(INavTaskAnnotationVisitor<T> visitor) {
            return visitor.VisitNavTriggerAnnotation(this);
        }
    }

    public abstract class NavTaskAnnotationVisitor: INavTaskAnnotationVisitor {

        public void Visit(NavTaskAnnotation annotation){
            annotation.Accept(this);
        }             

        protected virtual void DefaultVisit(NavTaskAnnotation annotation) {
        }

		public virtual void VisitNavTaskAnnotation(NavTaskAnnotation navTaskAnnotation) {
            DefaultVisit(navTaskAnnotation);
        }

		public virtual void VisitNavChoiceAnnotation(NavChoiceAnnotation navChoiceAnnotation) {
            DefaultVisit(navChoiceAnnotation);
        }

		public virtual void VisitNavExitAnnotation(NavExitAnnotation navExitAnnotation) {
            DefaultVisit(navExitAnnotation);
        }

		public virtual void VisitNavInitAnnotation(NavInitAnnotation navInitAnnotation) {
            DefaultVisit(navInitAnnotation);
        }

		public virtual void VisitNavInitCallAnnotation(NavInitCallAnnotation navInitCallAnnotation) {
            DefaultVisit(navInitCallAnnotation);
        }

		public virtual void VisitNavTriggerAnnotation(NavTriggerAnnotation navTriggerAnnotation) {
            DefaultVisit(navTriggerAnnotation);
        }

        }

        public abstract class NavTaskAnnotationVisitor<T>: INavTaskAnnotationVisitor<T> {

        public T Visit(NavTaskAnnotation annotation){
            return annotation.Accept(this);
        }             

        protected virtual T DefaultVisit(NavTaskAnnotation annotation) {
            return default(T);
        }

		public virtual T VisitNavTaskAnnotation(NavTaskAnnotation navTaskAnnotation) {
            return DefaultVisit(navTaskAnnotation);
        }

		public virtual T VisitNavChoiceAnnotation(NavChoiceAnnotation navChoiceAnnotation) {
            return DefaultVisit(navChoiceAnnotation);
        }

		public virtual T VisitNavExitAnnotation(NavExitAnnotation navExitAnnotation) {
            return DefaultVisit(navExitAnnotation);
        }

		public virtual T VisitNavInitAnnotation(NavInitAnnotation navInitAnnotation) {
            return DefaultVisit(navInitAnnotation);
        }

		public virtual T VisitNavInitCallAnnotation(NavInitCallAnnotation navInitCallAnnotation) {
            return DefaultVisit(navInitCallAnnotation);
        }

		public virtual T VisitNavTriggerAnnotation(NavTriggerAnnotation navTriggerAnnotation) {
            return DefaultVisit(navTriggerAnnotation);
        }

    }
}
